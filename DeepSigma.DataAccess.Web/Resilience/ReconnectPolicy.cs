namespace DeepSigma.DataAccess.Web.Resilience;

/// <summary>
/// Capped exponential-backoff-with-jitter schedule, shared by the HTTP retry handler and
/// (going forward) the SSE and WebSocket reconnection loops. Pure and allocation-free:
/// <see cref="GetDelay"/> computes a delay from an attempt number and does not touch the clock —
/// callers wait using their own <see cref="TimeProvider"/>, so timing stays testable at the call site.
/// </summary>
public sealed class ReconnectPolicy
{
    /// <summary>Maximum number of attempts (including the initial one). Use <see cref="int.MaxValue"/> for "retry indefinitely".</summary>
    public int MaxAttempts { get; init; } = 5;

    /// <summary>Base delay; the first backoff. Doubles each attempt before jitter and capping.</summary>
    public TimeSpan BaseBackoff { get; init; } = TimeSpan.FromSeconds(2);

    /// <summary>Hard ceiling on any single computed delay.</summary>
    public TimeSpan MaxBackoff { get; init; } = TimeSpan.FromMinutes(10);

    /// <summary>A policy with the default values (5 attempts, 2s base, 10m cap).</summary>
    public static ReconnectPolicy Default { get; } = new();

    /// <summary>
    /// Computes the delay to wait before <paramref name="attempt"/> (1-based). Exponential in the
    /// attempt number with additive jitter in <c>[0, BaseBackoff)</c>, clamped to <see cref="MaxBackoff"/>.
    /// </summary>
    public TimeSpan GetDelay(int attempt)
    {
        if (attempt < 1) { attempt = 1; }

        double exponentMs = BaseBackoff.TotalMilliseconds * Math.Pow(2, attempt - 1);
        int jitterUpperBound = Math.Max(1, (int)Math.Min(int.MaxValue, BaseBackoff.TotalMilliseconds));
        double jitterMs = Random.Shared.Next(0, jitterUpperBound);
        return Cap(TimeSpan.FromMilliseconds(exponentMs + jitterMs));
    }

    /// <summary>Clamps <paramref name="value"/> to <see cref="MaxBackoff"/>. Useful for externally supplied delays (e.g. a server <c>Retry-After</c>).</summary>
    public TimeSpan Cap(TimeSpan value) => value > MaxBackoff ? MaxBackoff : value;
}
