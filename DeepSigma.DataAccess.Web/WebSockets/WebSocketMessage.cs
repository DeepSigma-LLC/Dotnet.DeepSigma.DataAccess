using System.Net.WebSockets;

namespace DeepSigma.DataAccess.Web;

/// <summary>A received text frame (reassembled from any continuation frames), decoded as UTF-8.</summary>
public sealed record class WebSocketTextMessage(string Text);

/// <summary>A received binary frame (reassembled from any continuation frames).</summary>
public sealed record class WebSocketBinaryMessage(ReadOnlyMemory<byte> Bytes);

/// <summary>The peer's close frame, carrying the close status and description when present.</summary>
public sealed record class WebSocketCloseMessage(WebSocketCloseStatus? Status, string? Description);

/// <summary>
/// A message read from a <see cref="IWebSocketConnection"/>: exactly one of a text frame, a binary
/// frame, or the peer's close frame. Switch over it exhaustively:
/// <code>
/// string described = message switch
/// {
///     WebSocketTextMessage t   => t.Text,
///     WebSocketBinaryMessage b => $"{b.Bytes.Length} bytes",
///     WebSocketCloseMessage c  => $"closed: {c.Status}",
///     null                     => "(empty)",
/// };
/// </code>
/// (A C# 15 union — the case types are the records above.)
/// </summary>
public union WebSocketMessage(WebSocketTextMessage, WebSocketBinaryMessage, WebSocketCloseMessage);
