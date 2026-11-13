using System.Net.ServerSentEvents;

namespace DeepSigma.DataAccess.Web;

/// <summary>
/// Client for consuming Server-Sent Events (<c>text/event-stream</c>) with optional transparent
/// reconnection and <c>Last-Event-ID</c> resumption. Backed by <see cref="SseClient"/>; register via
/// <c>services.AddDeepSigmaSse()</c>. Depend on this interface so consumers can be unit-tested with a mock.
/// </summary>
public interface ISseClient
{
    /// <summary>
    /// Subscribes to the event stream at <paramref name="url"/> and yields each event as it arrives,
    /// with its data decoded as a UTF-8 string. The stream is long-lived: enumeration runs until the
    /// server closes without reconnect, <paramref name="cancellationToken"/> fires, or (with
    /// reconnection enabled) the reconnect budget is exhausted.
    /// </summary>
    IAsyncEnumerable<SseItem<string>> SubscribeAsync(string url, SseSubscribeOptions? options = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Subscribes to the event stream at <paramref name="url"/> and yields each event's data
    /// deserialized from JSON as <typeparamref name="T"/>, using the client's configured
    /// <see cref="System.Text.Json.JsonSerializerOptions"/>. Events whose data deserializes to null are skipped.
    /// </summary>
    IAsyncEnumerable<T> SubscribeAsync<T>(string url, SseSubscribeOptions? options = null, CancellationToken cancellationToken = default);
}
