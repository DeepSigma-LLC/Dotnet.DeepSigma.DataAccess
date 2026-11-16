using System.Net.Http.Headers;

namespace DeepSigma.DataAccess.Web.Throttling;

/// <summary>
/// Reads a response's <c>Retry-After</c> header as a wait: delta-seconds as given, an HTTP-date as the time from now.
/// The one reading used by <see cref="RetryAfterDelegatingHandler"/> and <see cref="HttpApiResponse.RetryAfter"/>, and
/// by a client that reports the wait to its caller instead of waiting itself.
/// </summary>
public static class RetryAfterHeader
{
    /// <summary>How long the response asks to wait; null when it has no <c>Retry-After</c>, or one already past or zero.</summary>
    /// <param name="response">The response.</param>
    /// <param name="clock">What "now" is for an HTTP-date; <see cref="TimeProvider.System"/> when null.</param>
    public static TimeSpan? GetDelay(HttpResponseMessage response, TimeProvider? clock = null)
    {
        ArgumentNullException.ThrowIfNull(response);
        return GetDelay(response.Headers, clock);
    }

    /// <inheritdoc cref="GetDelay(HttpResponseMessage, TimeProvider?)"/>
    /// <param name="headers">The response's headers.</param>
    /// <param name="clock">What "now" is for an HTTP-date; <see cref="TimeProvider.System"/> when null.</param>
    public static TimeSpan? GetDelay(HttpResponseHeaders headers, TimeProvider? clock = null)
    {
        ArgumentNullException.ThrowIfNull(headers);
        return headers.RetryAfter switch
        {
            { Delta: { } delta } when delta > TimeSpan.Zero => delta,
            { Date: { } date } when date - (clock ?? TimeProvider.System).GetUtcNow() is { } wait && wait > TimeSpan.Zero => wait,
            _ => null,
        };
    }
}
