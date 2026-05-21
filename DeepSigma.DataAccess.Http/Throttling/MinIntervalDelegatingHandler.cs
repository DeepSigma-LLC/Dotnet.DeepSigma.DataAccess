using System.Collections.Concurrent;

namespace DeepSigma.DataAccess.Http.Throttling;

/// <summary>
/// Configuration for <see cref="MinIntervalDelegatingHandler"/>.
/// </summary>
public sealed class MinIntervalOptions
{
    /// <summary>Minimum time that must elapse between two outgoing requests sharing the same <see cref="Key"/>.</summary>
    public TimeSpan MinInterval { get; init; }

    /// <summary>
    /// Throttle key. All handler instances using the same key share one process-wide throttle state,
    /// so multiple <see cref="HttpClient"/> instances (e.g. those produced by <see cref="IHttpClientFactory"/>)
    /// cooperate on spacing. Defaults to <c>"default"</c>.
    /// </summary>
    public string Key { get; init; } = "default";
}

/// <summary>
/// Delegating handler that enforces a minimum interval between outbound HTTP requests sharing a key.
/// Process-scoped only — does not coordinate across processes.
/// </summary>
/// <remarks>
/// Useful for APIs with strict pacing requirements (e.g. arXiv's 3s rule). Couple with a
/// <see cref="RetryAfterDelegatingHandler"/> to additionally honor server-driven backoff signals.
/// </remarks>
public sealed class MinIntervalDelegatingHandler : DelegatingHandler
{
    private static readonly ConcurrentDictionary<string, ThrottleState> _states = new();

    private readonly MinIntervalOptions _options;
    private readonly TimeProvider _clock;

    /// <summary>
    /// Creates a new handler. <paramref name="clock"/> defaults to <see cref="TimeProvider.System"/>;
    /// inject a fake <see cref="TimeProvider"/> in tests to avoid real waits.
    /// </summary>
    public MinIntervalDelegatingHandler(MinIntervalOptions options, TimeProvider? clock = null)
    {
        ArgumentNullException.ThrowIfNull(options);
        if (string.IsNullOrWhiteSpace(options.Key))
        {
            throw new ArgumentException("MinIntervalOptions.Key must be non-empty.", nameof(options));
        }
        _options = options;
        _clock = clock ?? TimeProvider.System;
    }

    /// <inheritdoc />
    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        ThrottleState state = _states.GetOrAdd(_options.Key, _ => new ThrottleState());

        await state.Gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            DateTimeOffset now = _clock.GetUtcNow();
            TimeSpan elapsed = now - state.LastDispatchedUtc;
            TimeSpan wait = _options.MinInterval - elapsed;
            if (wait > TimeSpan.Zero)
            {
                await Task.Delay(wait, _clock, cancellationToken).ConfigureAwait(false);
            }
            state.LastDispatchedUtc = _clock.GetUtcNow();
        }
        finally
        {
            state.Gate.Release();
        }

        return await base.SendAsync(request, cancellationToken).ConfigureAwait(false);
    }

    private sealed class ThrottleState
    {
        public SemaphoreSlim Gate { get; } = new(1, 1);
        public DateTimeOffset LastDispatchedUtc { get; set; } = DateTimeOffset.MinValue;
    }
}
