using System.Net.WebSockets;

namespace DeepSigma.DataAccess.Web;

/// <summary>
/// Opens <see cref="IWebSocketConnection"/>s. Register via <c>services.AddDeepSigmaWebSockets()</c>
/// and inject this factory; depend on the interface so consumers can be unit-tested with a mock.
/// </summary>
public interface IWebSocketConnectionFactory
{
    /// <summary>
    /// Connects to <paramref name="url"/> (a <c>ws://</c> or <c>wss://</c> endpoint) and returns an
    /// open, reading connection. Throws if the initial connect fails.
    /// </summary>
    Task<IWebSocketConnection> ConnectAsync(Uri url, WebSocketConnectionOptions? options = null, CancellationToken cancellationToken = default);
}

/// <summary>
/// A full-duplex WebSocket connection: typed/raw sends, a single reassembled receive stream, lifecycle
/// state, and (optionally) transparent reconnection. Dispose it to close and release the socket.
/// Intended for a single receive consumer; sends are serialized internally and safe to call concurrently.
/// </summary>
public interface IWebSocketConnection : IAsyncDisposable
{
    /// <summary>Current lifecycle state.</summary>
    WebSocketConnectionState State { get; }

    /// <summary>Raised whenever <see cref="State"/> changes. Handlers run on the receive loop; keep them quick.</summary>
    event Action<WebSocketConnectionState>? StateChanged;

    /// <summary>Serializes <paramref name="message"/> to JSON (using the client's options) and sends it as a text frame.</summary>
    Task SendAsync<T>(T message, CancellationToken cancellationToken = default);

    /// <summary>Sends <paramref name="text"/> as a UTF-8 text frame.</summary>
    Task SendTextAsync(string text, CancellationToken cancellationToken = default);

    /// <summary>Sends <paramref name="bytes"/> as a binary frame.</summary>
    Task SendBinaryAsync(ReadOnlyMemory<byte> bytes, CancellationToken cancellationToken = default);

    /// <summary>
    /// Yields each received message (text, binary, or the peer's close) as it is reassembled.
    /// Completes when the connection closes or reconnection is exhausted; a transport fault surfaces
    /// as an exception from enumeration only when reconnection is disabled.
    /// </summary>
    IAsyncEnumerable<WebSocketMessage> ReceiveAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Yields each text/binary message deserialized from JSON as <typeparamref name="T"/> (using the
    /// client's options); close frames end the stream and messages that deserialize to null are skipped.
    /// </summary>
    IAsyncEnumerable<T> ReceiveAsync<T>(CancellationToken cancellationToken = default);

    /// <summary>Completes the close handshake and transitions to <see cref="WebSocketConnectionState.Closed"/>.</summary>
    Task CloseAsync(WebSocketCloseStatus status = WebSocketCloseStatus.NormalClosure, string? description = null, CancellationToken cancellationToken = default);
}
