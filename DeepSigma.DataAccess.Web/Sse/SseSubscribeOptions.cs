using DeepSigma.DataAccess.Web.Resilience;

namespace DeepSigma.DataAccess.Web;

/// <summary>
/// Options for an <see cref="ISseClient"/> subscription (a <c>text/event-stream</c> consumption).
/// </summary>
public sealed class SseSubscribeOptions
{
    /// <summary>
    /// When <see langword="true"/> (default), the client transparently reconnects after the stream
    /// ends or the transport drops, resuming from the last seen event id and honoring the server's
    /// <c>retry:</c> hint. When <see langword="false"/>, enumeration simply completes when the stream ends.
    /// </summary>
    public bool AutoReconnect { get; set; } = true;

    /// <summary>
    /// Backoff schedule used between reconnect attempts. A transport fault counts as an attempt and
    /// is capped by <see cref="ReconnectPolicy.MaxAttempts"/>; set it to <see cref="int.MaxValue"/> to
    /// reconnect indefinitely. Defaults to <see cref="ReconnectPolicy.Default"/>.
    /// </summary>
    public ReconnectPolicy Reconnect { get; set; } = ReconnectPolicy.Default;

    /// <summary>
    /// Seed value for the <c>Last-Event-ID</c> header on the first connect, to resume a prior stream.
    /// The client keeps it current from each event's id thereafter.
    /// </summary>
    public string? LastEventId { get; set; }

    /// <summary>
    /// When set, only events whose <c>event:</c> type equals this value (ordinal) are yielded.
    /// Other events still advance the last-event-id and reconnect hint. Null yields every event.
    /// </summary>
    public string? EventTypeFilter { get; set; }

    /// <summary>
    /// Hook to mutate each outgoing request before it is sent (authorization headers, query, etc.).
    /// Invoked on the initial connect and on every reconnect.
    /// </summary>
    public Action<HttpRequestMessage>? ConfigureRequest { get; set; }
}
