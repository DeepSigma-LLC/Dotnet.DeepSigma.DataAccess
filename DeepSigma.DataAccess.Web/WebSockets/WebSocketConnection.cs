using System.Buffers;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using System.Threading.Channels;
using DeepSigma.DataAccess.Web.Resilience;
using Microsoft.Extensions.Logging;

namespace DeepSigma.DataAccess.Web;

/// <summary>
/// Default <see cref="IWebSocketConnection"/>. A single background loop reads frames from the socket,
/// reassembles them into whole messages, and publishes them to a bounded channel (back-pressure to the
/// socket when the consumer lags). On a transport fault it reconnects per the <see cref="ReconnectPolicy"/>
/// and runs the re-subscribe hook; sends are serialized behind a gate because a <see cref="WebSocket"/>
/// does not allow concurrent sends.
/// </summary>
internal sealed class WebSocketConnection : IWebSocketConnection
{
    private readonly Uri _url;
    private readonly WebSocketConnectionOptions _options;
    private readonly Func<Uri, WebSocketConnectionOptions, CancellationToken, Task<WebSocket>> _connector;
    private readonly JsonSerializerOptions _jsonOptions;
    private readonly ILogger _logger;
    private readonly TimeProvider _clock;

    private readonly Channel<WebSocketMessage> _channel;
    private readonly CancellationTokenSource _lifetime = new();
    private readonly SemaphoreSlim _sendGate = new(1, 1);

    private volatile WebSocket? _socket;
    private volatile WebSocketConnectionState _state = WebSocketConnectionState.Connecting;
    private Task _receiveLoop = Task.CompletedTask;

    public WebSocketConnection(
        Uri url,
        WebSocketConnectionOptions options,
        Func<Uri, WebSocketConnectionOptions, CancellationToken, Task<WebSocket>> connector,
        JsonSerializerOptions jsonOptions,
        ILogger logger,
        TimeProvider clock)
    {
        _url = url;
        _options = options;
        _connector = connector;
        _jsonOptions = jsonOptions;
        _logger = logger;
        _clock = clock;
        _channel = Channel.CreateBounded<WebSocketMessage>(new BoundedChannelOptions(Math.Max(1, options.ReceiveQueueCapacity))
        {
            FullMode = BoundedChannelFullMode.Wait,
            SingleWriter = true,
            SingleReader = false,
        });
    }

    public WebSocketConnectionState State => _state;
    public event Action<WebSocketConnectionState>? StateChanged;

    /// <summary>Performs the initial connect (surfacing failures to the caller) and starts the receive loop.</summary>
    internal async Task StartAsync(CancellationToken cancellationToken)
    {
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, _lifetime.Token);
        _socket = await _connector(_url, _options, linked.Token).ConfigureAwait(false);
        SetState(WebSocketConnectionState.Open);
        _receiveLoop = Task.Run(() => ReceiveLoopAsync(_lifetime.Token));
    }

    public IAsyncEnumerable<WebSocketMessage> ReceiveAsync(CancellationToken cancellationToken = default)
        => _channel.Reader.ReadAllAsync(cancellationToken);

    public async IAsyncEnumerable<T> ReceiveAsync<T>([System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        await foreach (WebSocketMessage message in _channel.Reader.ReadAllAsync(cancellationToken).ConfigureAwait(false))
        {
            switch (message)
            {
                case WebSocketTextMessage text:
                {
                    T? value = JsonSerializer.Deserialize<T>(text.Text, _jsonOptions);
                    if (value is not null) { yield return value; }
                    break;
                }
                case WebSocketBinaryMessage binary:
                {
                    T? value = JsonSerializer.Deserialize<T>(binary.Bytes.Span, _jsonOptions);
                    if (value is not null) { yield return value; }
                    break;
                }
                case WebSocketCloseMessage:
                    yield break;
            }
        }
    }

    public Task SendAsync<T>(T message, CancellationToken cancellationToken = default)
        => SendFrameAsync(JsonSerializer.SerializeToUtf8Bytes(message, _jsonOptions), WebSocketMessageType.Text, cancellationToken);

    public Task SendTextAsync(string text, CancellationToken cancellationToken = default)
        => SendFrameAsync(Encoding.UTF8.GetBytes(text), WebSocketMessageType.Text, cancellationToken);

    public Task SendBinaryAsync(ReadOnlyMemory<byte> bytes, CancellationToken cancellationToken = default)
        => SendFrameAsync(bytes, WebSocketMessageType.Binary, cancellationToken);

    private async Task SendFrameAsync(ReadOnlyMemory<byte> payload, WebSocketMessageType type, CancellationToken cancellationToken)
    {
        WebSocket? socket = _socket;
        if (_state != WebSocketConnectionState.Open || socket is null || socket.State != WebSocketState.Open)
        {
            throw new InvalidOperationException($"Cannot send: the WebSocket connection is {_state}.");
        }

        using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, _lifetime.Token);
        await _sendGate.WaitAsync(linked.Token).ConfigureAwait(false);
        try
        {
            await socket.SendAsync(payload, type, endOfMessage: true, linked.Token).ConfigureAwait(false);
        }
        finally
        {
            _sendGate.Release();
        }
    }

    private async Task ReceiveLoopAsync(CancellationToken ct)
    {
        try
        {
            while (!ct.IsCancellationRequested)
            {
                WebSocketMessage message;
                try
                {
                    message = await ReceiveOneAsync(ct).ConfigureAwait(false);
                }
                catch (OperationCanceledException) when (ct.IsCancellationRequested)
                {
                    break;
                }
                catch (Exception ex)
                {
                    // Transport fault. Reconnect if allowed, else surface the error to the consumer.
                    if (!_options.AutoReconnect || !await TryReconnectAsync(ex, ct).ConfigureAwait(false))
                    {
                        SetState(WebSocketConnectionState.Closed);
                        _channel.Writer.TryComplete(ex);
                        return;
                    }
                    continue;
                }

                await _channel.Writer.WriteAsync(message, ct).ConfigureAwait(false);

                if (message is WebSocketCloseMessage)
                {
                    // A clean close from the peer: stop, do not reconnect.
                    SetState(WebSocketConnectionState.Closed);
                    break;
                }
            }
        }
        catch (OperationCanceledException) { /* lifetime ended */ }
        finally
        {
            _channel.Writer.TryComplete();
        }
    }

    private async Task<WebSocketMessage> ReceiveOneAsync(CancellationToken ct)
    {
        WebSocket socket = _socket ?? throw new InvalidOperationException("No active socket.");
        byte[] buffer = ArrayPool<byte>.Shared.Rent(_options.ReceiveBufferSize);
        using var assembled = new MemoryStream();
        try
        {
            WebSocketMessageType type = WebSocketMessageType.Text;
            while (true)
            {
                ValueWebSocketReceiveResult result = await socket.ReceiveAsync(buffer.AsMemory(), ct).ConfigureAwait(false);
                if (result.MessageType == WebSocketMessageType.Close)
                {
                    return new WebSocketCloseMessage(socket.CloseStatus, socket.CloseStatusDescription);
                }

                assembled.Write(buffer, 0, result.Count);
                type = result.MessageType;
                if (result.EndOfMessage) { break; }
            }

            return type == WebSocketMessageType.Text
                ? new WebSocketTextMessage(Encoding.UTF8.GetString(assembled.GetBuffer(), 0, (int)assembled.Length))
                : new WebSocketBinaryMessage(assembled.ToArray());
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(buffer);
        }
    }

    private async Task<bool> TryReconnectAsync(Exception cause, CancellationToken ct)
    {
        _logger.LogDebug(cause, "WebSocket {Url} dropped; attempting to reconnect.", _url);
        SetState(WebSocketConnectionState.Reconnecting);

        ReconnectPolicy policy = _options.Reconnect ?? ReconnectPolicy.Default;
        int attempt = 0;
        while (!ct.IsCancellationRequested)
        {
            attempt++;
            if (attempt >= policy.MaxAttempts)
            {
                _logger.LogWarning(cause, "WebSocket {Url} reconnect budget exhausted after {Attempts} attempts.", _url, attempt);
                return false;
            }

            try
            {
                await Task.Delay(policy.GetDelay(attempt), _clock, ct).ConfigureAwait(false);
                WebSocket socket = await _connector(_url, _options, ct).ConfigureAwait(false);
                WebSocket? previous = Interlocked.Exchange(ref _socket, socket);
                previous?.Dispose();

                SetState(WebSocketConnectionState.Open);
                if (_options.OnReconnected is not null)
                {
                    await _options.OnReconnected(this, ct).ConfigureAwait(false);
                }
                return true;
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                return false;
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "WebSocket {Url} reconnect attempt {Attempt} failed.", _url, attempt);
            }
        }
        return false;
    }

    public async Task CloseAsync(WebSocketCloseStatus status = WebSocketCloseStatus.NormalClosure, string? description = null, CancellationToken cancellationToken = default)
    {
        SetState(WebSocketConnectionState.Closed);
        WebSocket? socket = _socket;
        if (socket is not null && (socket.State == WebSocketState.Open || socket.State == WebSocketState.CloseReceived))
        {
            try
            {
                await socket.CloseAsync(status, description, cancellationToken).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "WebSocket {Url} close handshake failed; aborting.", _url);
            }
        }

        await _lifetime.CancelAsync().ConfigureAwait(false);
        _channel.Writer.TryComplete();
    }

    public async ValueTask DisposeAsync()
    {
        SetState(WebSocketConnectionState.Closed);
        if (!_lifetime.IsCancellationRequested)
        {
            try { await _lifetime.CancelAsync().ConfigureAwait(false); } catch { /* best effort */ }
        }

        WebSocket? socket = _socket;
        if (socket is not null)
        {
            try { socket.Abort(); } catch { /* best effort */ }
            socket.Dispose();
        }

        _channel.Writer.TryComplete();
        try { await _receiveLoop.ConfigureAwait(false); } catch { /* already torn down */ }

        _lifetime.Dispose();
        _sendGate.Dispose();
    }

    private void SetState(WebSocketConnectionState next)
    {
        if (_state == next) { return; }
        _state = next;
        StateChanged?.Invoke(next);
    }
}
