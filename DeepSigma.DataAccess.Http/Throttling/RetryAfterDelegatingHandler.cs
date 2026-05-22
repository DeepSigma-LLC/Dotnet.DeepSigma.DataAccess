using System.Net;

namespace DeepSigma.DataAccess.Http.Throttling;

/// <summary>
/// Configuration for <see cref="RetryAfterDelegatingHandler"/>.
/// </summary>
public sealed class RetryAfterOptions
{
    /// <summary>Maximum number of attempts (including the initial one).</summary>
    public int MaxAttempts { get; set; } = 5;

    /// <summary>Base backoff used for exponential fallback when no <c>Retry-After</c> header is present.</summary>
    public TimeSpan BaseBackoff { get; set; } = TimeSpan.FromSeconds(2);

    /// <summary>Hard ceiling on a single backoff wait, regardless of header or computed value.</summary>
    public TimeSpan MaxBackoff { get; set; } = TimeSpan.FromMinutes(10);

    /// <summary>Status codes that should trigger a retry.</summary>
    public HashSet<HttpStatusCode> RetryStatusCodes { get; set; } = new()
    {
        (HttpStatusCode)429, // TooManyRequests
        HttpStatusCode.ServiceUnavailable,
    };
}

/// <summary>
/// Delegating handler that retries on configured status codes, honoring <c>Retry-After</c>
/// (delta-seconds or HTTP-date) and falling back to capped exponential backoff with jitter.
/// </summary>
public sealed class RetryAfterDelegatingHandler : DelegatingHandler
{
    private readonly RetryAfterOptions _options;
    private readonly TimeProvider _clock;

    /// <summary>
    /// Creates a new handler. <paramref name="clock"/> defaults to <see cref="TimeProvider.System"/>;
    /// inject a fake <see cref="TimeProvider"/> in tests to avoid real waits.
    /// </summary>
    public RetryAfterDelegatingHandler(RetryAfterOptions options, TimeProvider? clock = null)
    {
        ArgumentNullException.ThrowIfNull(options);
        if (options.MaxAttempts < 1)
        {
            throw new ArgumentException("MaxAttempts must be >= 1.", nameof(options));
        }
        _options = options;
        _clock = clock ?? TimeProvider.System;
    }

    /// <inheritdoc />
    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        // We may need to send the same logical request multiple times. HttpClient disposes
        // HttpContent and consumes the request after each send, so we buffer the content
        // upfront (if any) and clone a fresh request per attempt. The original `request` instance
        // is sent on the first attempt; subsequent attempts use a clone.
        await BufferContentForRetryAsync(request, cancellationToken).ConfigureAwait(false);

        HttpResponseMessage? response = null;
        for (int attempt = 1; attempt <= _options.MaxAttempts; attempt++)
        {
            response?.Dispose();

            HttpRequestMessage attemptRequest = attempt == 1 ? request : await CloneRequestAsync(request, cancellationToken).ConfigureAwait(false);
            response = await base.SendAsync(attemptRequest, cancellationToken).ConfigureAwait(false);

            // Dispose of the cloned request after the send; the inner handler has consumed it.
            // The original request (attempt 1) is owned by the caller and disposed elsewhere.
            if (attempt > 1) { attemptRequest.Dispose(); }

            if (!_options.RetryStatusCodes.Contains(response.StatusCode)) { return response; }
            if (attempt == _options.MaxAttempts) { return response; }

            TimeSpan delay = ComputeDelay(response, attempt);
            await Task.Delay(delay, _clock, cancellationToken).ConfigureAwait(false);
        }
        return response!;
    }

    /// <summary>
    /// Eagerly buffers the request content (if any) so it can be re-read on retries.
    /// No-op for GET/HEAD/DELETE without a body — the common case.
    /// </summary>
    private static async Task BufferContentForRetryAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        if (request.Content is null) { return; }
        // LoadIntoBufferAsync reads the content into an internal buffer once; subsequent
        // calls to CopyToAsync (which CloneRequestAsync does) read from the buffer rather
        // than re-running whatever stream produced the original content.
        await request.Content.LoadIntoBufferAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Produces a fresh <see cref="HttpRequestMessage"/> with the same method, URI, version,
    /// headers, options, and content (copied from the buffered source) as the original.
    /// </summary>
    private static async Task<HttpRequestMessage> CloneRequestAsync(HttpRequestMessage source, CancellationToken cancellationToken)
    {
        var clone = new HttpRequestMessage(source.Method, source.RequestUri)
        {
            Version = source.Version,
            VersionPolicy = source.VersionPolicy,
        };

        foreach (var header in source.Headers)
        {
            clone.Headers.TryAddWithoutValidation(header.Key, header.Value);
        }

        foreach (var option in source.Options)
        {
            // HttpRequestOptionsKey<T> is opaque at this layer; preserve via the IDictionary<string, object?> view.
            ((IDictionary<string, object?>)clone.Options)[option.Key] = option.Value;
        }

        if (source.Content is not null)
        {
            var buffer = new MemoryStream();
            await source.Content.CopyToAsync(buffer, cancellationToken).ConfigureAwait(false);
            buffer.Position = 0;

            var cloneContent = new StreamContent(buffer);
            foreach (var header in source.Content.Headers)
            {
                cloneContent.Headers.TryAddWithoutValidation(header.Key, header.Value);
            }
            clone.Content = cloneContent;
        }

        return clone;
    }

    private TimeSpan ComputeDelay(HttpResponseMessage response, int attempt)
    {
        // Honor Retry-After header when present.
        System.Net.Http.Headers.RetryConditionHeaderValue? retryAfter = response.Headers.RetryAfter;
        if (retryAfter is not null)
        {
            if (retryAfter.Delta is { } delta && delta > TimeSpan.Zero)
            {
                return Cap(delta);
            }
            if (retryAfter.Date is { } date)
            {
                TimeSpan diff = date - _clock.GetUtcNow();
                if (diff > TimeSpan.Zero) { return Cap(diff); }
            }
        }

        // Capped exponential backoff with jitter.
        double exponentMs = _options.BaseBackoff.TotalMilliseconds * Math.Pow(2, attempt - 1);
        int jitterUpperBound = Math.Max(1, (int)Math.Min(int.MaxValue, _options.BaseBackoff.TotalMilliseconds));
        double jitterMs = Random.Shared.Next(0, jitterUpperBound);
        return Cap(TimeSpan.FromMilliseconds(exponentMs + jitterMs));
    }

    private TimeSpan Cap(TimeSpan value) => value > _options.MaxBackoff ? _options.MaxBackoff : value;
}
