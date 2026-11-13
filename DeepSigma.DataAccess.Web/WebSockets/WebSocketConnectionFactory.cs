using System.Net.WebSockets;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace DeepSigma.DataAccess.Web;

/// <summary>
/// Default <see cref="IWebSocketConnectionFactory"/>: opens connections backed by
/// <see cref="ClientWebSocket"/>. Register with <c>services.AddDeepSigmaWebSockets()</c>.
/// </summary>
public sealed class WebSocketConnectionFactory : IWebSocketConnectionFactory
{
    private readonly Func<Uri, WebSocketConnectionOptions, CancellationToken, Task<WebSocket>> _connector;
    private readonly JsonSerializerOptions _jsonOptions;
    private readonly ILoggerFactory _loggerFactory;
    private readonly TimeProvider _clock;

    /// <summary>
    /// Creates a factory that connects with <see cref="ClientWebSocket"/>. Pass
    /// <see cref="Serializer.SnakeCaseJsonOptions"/> (or custom options) to override the default JSON shape.
    /// </summary>
    public WebSocketConnectionFactory(JsonSerializerOptions? jsonOptions = null, ILoggerFactory? loggerFactory = null, TimeProvider? timeProvider = null)
        : this(DefaultConnector, jsonOptions, loggerFactory, timeProvider)
    {
    }

    /// <summary>
    /// Test/advanced seam: supply the socket connector directly (e.g. an in-memory
    /// <see cref="WebSocket"/>) instead of connecting a real <see cref="ClientWebSocket"/>.
    /// </summary>
    internal WebSocketConnectionFactory(
        Func<Uri, WebSocketConnectionOptions, CancellationToken, Task<WebSocket>> connector,
        JsonSerializerOptions? jsonOptions = null,
        ILoggerFactory? loggerFactory = null,
        TimeProvider? timeProvider = null)
    {
        _connector = connector;
        _jsonOptions = jsonOptions ?? Serializer.DefaultJsonOptions;
        _loggerFactory = loggerFactory ?? NullLoggerFactory.Instance;
        _clock = timeProvider ?? TimeProvider.System;
    }

    /// <inheritdoc />
    public async Task<IWebSocketConnection> ConnectAsync(Uri url, WebSocketConnectionOptions? options = null, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(url);
        var connection = new WebSocketConnection(
            url,
            options ?? new WebSocketConnectionOptions(),
            _connector,
            _jsonOptions,
            _loggerFactory.CreateLogger<WebSocketConnection>(),
            _clock);

        await connection.StartAsync(cancellationToken).ConfigureAwait(false);
        return connection;
    }

    private static async Task<WebSocket> DefaultConnector(Uri url, WebSocketConnectionOptions options, CancellationToken cancellationToken)
    {
        var socket = new ClientWebSocket();
        socket.Options.KeepAliveInterval = options.KeepAliveInterval;
        foreach (string subProtocol in options.SubProtocols)
        {
            socket.Options.AddSubProtocol(subProtocol);
        }
        options.ConfigureSocket?.Invoke(socket.Options);

        try
        {
            await socket.ConnectAsync(url, cancellationToken).ConfigureAwait(false);
        }
        catch
        {
            socket.Dispose();
            throw;
        }
        return socket;
    }
}
