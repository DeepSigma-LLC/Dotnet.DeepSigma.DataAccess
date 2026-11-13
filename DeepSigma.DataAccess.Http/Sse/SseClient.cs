using System.Net.ServerSentEvents;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using DeepSigma.DataAccess.Web.Resilience;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace DeepSigma.DataAccess.Web;

/// <summary>
/// Consumes Server-Sent Event streams over <see cref="HttpClient"/>, parsing the wire format with
/// <see cref="SseParser"/> and layering optional reconnection on top: on stream end or transport
/// fault it reconnects (when <see cref="SseSubscribeOptions.AutoReconnect"/> is set), sending the
/// last seen event id as <c>Last-Event-ID</c> and spacing attempts with a <see cref="ReconnectPolicy"/>,
/// floored by the server's <c>retry:</c> hint.
/// </summary>
/// <remarks>
/// Register with <c>services.AddDeepSigmaSse()</c>. No per-call timeout is applied: an SSE stream is
/// meant to stay open, so its lifetime is bounded by the caller's <see cref="CancellationToken"/>.
/// The connect phase is still bounded by the underlying <see cref="HttpClient.Timeout"/> because the
/// request uses <see cref="HttpCompletionOption.ResponseHeadersRead"/>.
/// </remarks>
public sealed class SseClient : ISseClient
{
    private static readonly SseItemParser<string> StringParser = static (_, bytes) => Encoding.UTF8.GetString(bytes);

    private readonly HttpClient _http;
    private readonly ILogger<SseClient> _logger;
    private readonly JsonSerializerOptions _jsonOptions;
    private readonly TimeProvider _clock;

    /// <summary>
    /// Initializes a new instance. Pass <see cref="Serializer.SnakeCaseJsonOptions"/> (or any custom
    /// options) to override <see cref="Serializer.DefaultJsonOptions"/>; inject a fake
    /// <see cref="TimeProvider"/> in tests to drive reconnection delays without real waits.
    /// </summary>
    public SseClient(HttpClient httpClient, ILogger<SseClient>? logger = null, JsonSerializerOptions? jsonOptions = null, TimeProvider? timeProvider = null)
    {
        _http = httpClient;
        _logger = logger ?? NullLogger<SseClient>.Instance;
        _jsonOptions = jsonOptions ?? Serializer.DefaultJsonOptions;
        _clock = timeProvider ?? TimeProvider.System;
    }

    /// <inheritdoc />
    public IAsyncEnumerable<SseItem<string>> SubscribeAsync(string url, SseSubscribeOptions? options = null, CancellationToken cancellationToken = default)
        => SubscribeItemsAsync(url, options ?? new SseSubscribeOptions(), StringParser, cancellationToken);

    /// <inheritdoc />
    public async IAsyncEnumerable<T> SubscribeAsync<T>(string url, SseSubscribeOptions? options = null, [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        SseItemParser<T?> parser = (_, bytes) => JsonSerializer.Deserialize<T>(bytes, _jsonOptions);

        await foreach (SseItem<T?> item in SubscribeItemsAsync(url, options ?? new SseSubscribeOptions(), parser, cancellationToken).ConfigureAwait(false))
        {
            if (item.Data is { } data) { yield return data; }
        }
    }

    /// <summary>
    /// Reconnecting enumeration core: opens the stream, parses it with <see cref="SseParser"/>, and on
    /// end/fault reconnects per <paramref name="options"/>. <c>yield return</c> cannot sit inside a
    /// try/catch, so the per-item read is wrapped manually and faults break out to the reconnect logic.
    /// </summary>
    private async IAsyncEnumerable<SseItem<TData>> SubscribeItemsAsync<TData>(
        string url,
        SseSubscribeOptions options,
        SseItemParser<TData> itemParser,
        [EnumeratorCancellation] CancellationToken ct)
    {
        string? lastEventId = string.IsNullOrEmpty(options.LastEventId) ? null : options.LastEventId;
        ReconnectPolicy policy = options.Reconnect ?? ReconnectPolicy.Default;
        int attempt = 0;

        while (true)
        {
            ct.ThrowIfCancellationRequested();

            HttpResponseMessage? response = null;
            Stream? stream = null;
            try
            {
                var request = new HttpRequestMessage(HttpMethod.Get, url);
                request.Headers.TryAddWithoutValidation("Accept", "text/event-stream");
                request.Headers.TryAddWithoutValidation("Cache-Control", "no-cache");
                if (lastEventId is not null) { request.Headers.TryAddWithoutValidation("Last-Event-ID", lastEventId); }
                options.ConfigureRequest?.Invoke(request);

                response = await _http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, ct).ConfigureAwait(false);
                response.EnsureSuccessStatusCode();
                stream = await response.Content.ReadAsStreamAsync(ct).ConfigureAwait(false);
                attempt = 0; // a successful connect resets the backoff
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                response?.Dispose();
                throw;
            }
            catch (Exception ex) when (options.AutoReconnect)
            {
                response?.Dispose();
                attempt++;
                if (attempt >= policy.MaxAttempts)
                {
                    _logger.LogWarning(ex, "SSE connect to {Url} failed; reconnect budget exhausted after {Attempts} attempts.", url, attempt);
                    throw;
                }
                _logger.LogDebug(ex, "SSE connect to {Url} failed (attempt {Attempt}); backing off.", url, attempt);
                await Task.Delay(policy.GetDelay(attempt), _clock, ct).ConfigureAwait(false);
                continue;
            }

            bool faulted = false;
            TimeSpan? reconnectHint = null;
            SseParser<TData> parser = SseParser.Create(stream, itemParser);
            IAsyncEnumerator<SseItem<TData>> enumerator = parser.EnumerateAsync(ct).GetAsyncEnumerator(ct);
            try
            {
                while (true)
                {
                    SseItem<TData> item;
                    try
                    {
                        if (!await enumerator.MoveNextAsync().ConfigureAwait(false)) { break; }
                        item = enumerator.Current;
                    }
                    catch (OperationCanceledException) when (ct.IsCancellationRequested) { throw; }
                    catch (Exception ex) when (options.AutoReconnect)
                    {
                        _logger.LogDebug(ex, "SSE stream from {Url} faulted mid-read; will reconnect.", url);
                        faulted = true;
                        break;
                    }

                    if (item.EventId is { Length: > 0 }) { lastEventId = item.EventId; }
                    if (item.ReconnectionInterval is { } ri) { reconnectHint = ri; }

                    if (options.EventTypeFilter is null ||
                        string.Equals(item.EventType, options.EventTypeFilter, StringComparison.Ordinal))
                    {
                        yield return item;
                    }
                }
            }
            finally
            {
                await enumerator.DisposeAsync().ConfigureAwait(false);
                stream.Dispose();
                response.Dispose();
            }

            if (!options.AutoReconnect) { yield break; }
            ct.ThrowIfCancellationRequested();

            if (faulted)
            {
                attempt++;
                if (attempt >= policy.MaxAttempts) { yield break; }
                TimeSpan wait = policy.Cap(reconnectHint ?? policy.GetDelay(attempt));
                await Task.Delay(wait, _clock, ct).ConfigureAwait(false);
            }
            else
            {
                // Clean end: the server closed deliberately. Reconnect, honoring its retry hint.
                attempt = 0;
                if (reconnectHint is { } hint)
                {
                    await Task.Delay(policy.Cap(hint), _clock, ct).ConfigureAwait(false);
                }
            }
        }
    }
}
