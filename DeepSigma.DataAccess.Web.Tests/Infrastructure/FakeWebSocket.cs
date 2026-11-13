using System.Net.WebSockets;
using System.Text;

namespace DeepSigma.DataAccess.Web.Tests.Infrastructure;

/// <summary>
/// In-memory <see cref="WebSocket"/> for tests. Scripts a sequence of receive steps (frames, a close,
/// or a thrown fault) and records every frame sent. Only the <see cref="Memory{T}"/>-based receive and
/// <see cref="ReadOnlyMemory{T}"/>-based send overloads — the ones <c>WebSocketConnection</c> uses — are implemented.
/// </summary>
internal sealed class FakeWebSocket : WebSocket
{
    private abstract record Step;
    private sealed record FrameStep(WebSocketMessageType Type, byte[] Data, bool EndOfMessage) : Step;
    private sealed record CloseStep(WebSocketCloseStatus Status, string? Description) : Step;
    private sealed record ThrowStep(Exception Error) : Step;

    private readonly Queue<Step> _steps = new();
    private readonly object _gate = new();
    private WebSocketState _state = WebSocketState.Open;
    private WebSocketCloseStatus? _closeStatus;
    private string? _closeDescription;

    public sealed record SentFrame(WebSocketMessageType Type, byte[] Data)
    {
        public string Text => Encoding.UTF8.GetString(Data);
    }

    public List<SentFrame> Sent { get; } = new();

    public FakeWebSocket QueueText(string text, bool endOfMessage = true)
    {
        lock (_gate) { _steps.Enqueue(new FrameStep(WebSocketMessageType.Text, Encoding.UTF8.GetBytes(text), endOfMessage)); }
        return this;
    }

    public FakeWebSocket QueueBinary(byte[] bytes, bool endOfMessage = true)
    {
        lock (_gate) { _steps.Enqueue(new FrameStep(WebSocketMessageType.Binary, bytes, endOfMessage)); }
        return this;
    }

    public FakeWebSocket QueueClose(WebSocketCloseStatus status = WebSocketCloseStatus.NormalClosure, string? description = null)
    {
        lock (_gate) { _steps.Enqueue(new CloseStep(status, description)); }
        return this;
    }

    public FakeWebSocket QueueThrow(Exception error)
    {
        lock (_gate) { _steps.Enqueue(new ThrowStep(error)); }
        return this;
    }

    public override async ValueTask<ValueWebSocketReceiveResult> ReceiveAsync(Memory<byte> buffer, CancellationToken cancellationToken)
    {
        Step? step;
        lock (_gate) { step = _steps.Count > 0 ? _steps.Dequeue() : null; }

        if (step is null)
        {
            // No more scripted input: behave like an open-but-idle socket until cancelled.
            await Task.Delay(Timeout.Infinite, cancellationToken).ConfigureAwait(false);
            throw new OperationCanceledException(cancellationToken);
        }

        switch (step)
        {
            case ThrowStep t:
                throw t.Error;
            case CloseStep c:
                _state = WebSocketState.CloseReceived;
                _closeStatus = c.Status;
                _closeDescription = c.Description;
                return new ValueWebSocketReceiveResult(0, WebSocketMessageType.Close, endOfMessage: true);
            case FrameStep f:
                f.Data.CopyTo(buffer.Span);
                return new ValueWebSocketReceiveResult(f.Data.Length, f.Type, f.EndOfMessage);
            default:
                throw new InvalidOperationException("Unknown step.");
        }
    }

    public override ValueTask SendAsync(ReadOnlyMemory<byte> buffer, WebSocketMessageType messageType, bool endOfMessage, CancellationToken cancellationToken)
    {
        Sent.Add(new SentFrame(messageType, buffer.ToArray()));
        return ValueTask.CompletedTask;
    }

    public override WebSocketCloseStatus? CloseStatus => _closeStatus;
    public override string? CloseStatusDescription => _closeDescription;
    public override WebSocketState State => _state;
    public override string? SubProtocol => null;

    public override void Abort() => _state = WebSocketState.Aborted;

    public override Task CloseAsync(WebSocketCloseStatus closeStatus, string? statusDescription, CancellationToken cancellationToken)
    {
        _state = WebSocketState.Closed;
        _closeStatus = closeStatus;
        _closeDescription = statusDescription;
        return Task.CompletedTask;
    }

    public override Task CloseOutputAsync(WebSocketCloseStatus closeStatus, string? statusDescription, CancellationToken cancellationToken)
    {
        _state = WebSocketState.CloseSent;
        return Task.CompletedTask;
    }

    public override void Dispose() => _state = WebSocketState.Closed;

    public override Task<WebSocketReceiveResult> ReceiveAsync(ArraySegment<byte> buffer, CancellationToken cancellationToken)
        => throw new NotSupportedException("Use the Memory-based ReceiveAsync.");

    public override Task SendAsync(ArraySegment<byte> buffer, WebSocketMessageType messageType, bool endOfMessage, CancellationToken cancellationToken)
        => throw new NotSupportedException("Use the ReadOnlyMemory-based SendAsync.");
}
