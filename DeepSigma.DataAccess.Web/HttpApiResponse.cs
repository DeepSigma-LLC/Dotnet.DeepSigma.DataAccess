using System.Net;

namespace DeepSigma.DataAccess.Web;

/// <summary>
/// A response read whole, whatever its status (<see cref="IHttpApi.SendForResponseAsync"/>): for an API whose refusals
/// carry their reason in the body (<c>400 Invalid key 'x'.</c>), or whose caller decides what a status means.
/// </summary>
/// <param name="StatusCode">The HTTP status.</param>
/// <param name="Body">The body as text; empty when there was none.</param>
/// <param name="Headers">The response's and its content's headers, by name (case-insensitive), each with its values.</param>
/// <param name="RetryAfter">How long a <c>Retry-After</c> asked to wait (<see cref="Throttling.RetryAfterHeader"/>); null
/// without one.</param>
public sealed record HttpApiResponse(HttpStatusCode StatusCode, string Body, IReadOnlyDictionary<string, IReadOnlyList<string>> Headers, TimeSpan? RetryAfter)
{
    /// <summary>Whether the status is 2xx.</summary>
    public bool IsSuccess => (int)StatusCode is >= 200 and <= 299;

    /// <summary>A header's first value; null when absent.</summary>
    public string? Header(string name) => Headers.TryGetValue(name, out var values) && values.Count > 0 ? values[0] : null;
}
