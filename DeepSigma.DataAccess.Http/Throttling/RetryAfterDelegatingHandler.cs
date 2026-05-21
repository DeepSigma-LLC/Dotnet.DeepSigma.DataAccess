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
        HttpResponseMessage? response = null;
        for (int attempt = 1; attempt <= _options.MaxAttempts; attempt++)
        {
            response?.Dispose();
            response = await base.SendAsync(request, cancellationToken).ConfigureAwait(false);

            if (!_options.RetryStatusCodes.Contains(response.StatusCode)) { return response; }
            if (attempt == _options.MaxAttempts) { return response; }

            TimeSpan delay = ComputeDelay(response, attempt);
            await Task.Delay(delay, _clock, cancellationToken).ConfigureAwait(false);
        }
        return response!;
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
