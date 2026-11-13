using System.Net.WebSockets;
using DeepSigma.DataAccess.Web.Resilience;

namespace DeepSigma.DataAccess.Web;

/// <summary>Lifecycle state of an <see cref="IWebSocketConnection"/>.</summary>
public enum WebSocketConnectionState
{
    /// <summary>The initial connect is in progress.</summary>
    Connecting,
    /// <summary>The socket is open and exchanging messages.</summary>
    Open,
    /// <summary>The socket dropped and a reconnect is in progress.</summary>
    Reconnecting,
    /// <summary>The connection is closed and will not reconnect.</summary>
    Closed,
}

/// <summary>Options controlling an <see cref="IWebSocketConnection"/>.</summary>
public sealed class WebSocketConnectionOptions
{
    /// <summary>
    /// When <see langword="true"/> (default), the connection reconnects after a transport fault,
    /// invoking <see cref="OnReconnected"/> so you can re-send subscription frames. A clean close
    /// (either peer sending a close frame) never reconnects.
    /// </summary>
    public bool AutoReconnect { get; set; } = true;

    /// <summary>Backoff schedule between reconnect attempts. Set <see cref="ReconnectPolicy.MaxAttempts"/> to <see cref="int.MaxValue"/> to reconnect indefinitely.</summary>
    public ReconnectPolicy Reconnect { get; set; } = ReconnectPolicy.Default;

    /// <summary>Keep-alive ping interval applied to the underlying <see cref="ClientWebSocket"/>. Defaults to 30s.</summary>
    public TimeSpan KeepAliveInterval { get; set; } = TimeSpan.FromSeconds(30);

    /// <summary>Size of the per-read buffer used while reassembling a message. Defaults to 8&#160;KiB.</summary>
    public int ReceiveBufferSize { get; set; } = 8192;

    /// <summary>
    /// Capacity of the bounded queue that buffers received messages for the consumer. When full, the
    /// receive loop blocks — applying back-pressure to the socket — rather than growing unbounded. Defaults to 1024.
    /// </summary>
    public int ReceiveQueueCapacity { get; set; } = 1024;

    /// <summary>Negotiated sub-protocols to offer during the handshake.</summary>
    public IReadOnlyList<string> SubProtocols { get; set; } = [];

    /// <summary>Hook to configure the underlying <see cref="ClientWebSocketOptions"/> (headers, auth, TLS, cookies) before each connect.</summary>
    public Action<ClientWebSocketOptions>? ConfigureSocket { get; set; }

    /// <summary>
    /// Invoked after a successful reconnect, before message delivery resumes, so you can re-establish
    /// server-side state (e.g. re-subscribe to symbols). Exceptions it throws abort the reconnect.
    /// </summary>
    public Func<IWebSocketConnection, CancellationToken, Task>? OnReconnected { get; set; }
}
