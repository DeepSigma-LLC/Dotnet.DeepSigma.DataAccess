using System.Net.WebSockets;
using DeepSigma.DataAccess.Web.Resilience;
using DeepSigma.DataAccess.Web.Tests.Infrastructure;
using Xunit;

// These tests drive connection lifetime explicitly (DisposeAsync / CloseAsync), so the analyzer's
// suggestion to thread TestContext's cancellation token does not apply.
#pragma warning disable xUnit1051

namespace DeepSigma.DataAccess.Web.Tests;

public class WebSocketConnection_Tests
{
    private static readonly Uri Url = new("wss://localhost/ws");

    private sealed record Dto
    {
        public string Value { get; init; } = "";
    }

    private static WebSocketConnectionFactory FactoryFor(params FakeWebSocket[] sockets)
    {
        var queue = new Queue<FakeWebSocket>(sockets);
        return new WebSocketConnectionFactory((_, _, _) =>
            queue.Count > 0
                ? Task.FromResult<WebSocket>(queue.Dequeue())
                : throw new WebSocketException("no more sockets"));
    }

    private static readonly ReconnectPolicy InstantReconnect =
        new() { MaxAttempts = 5, BaseBackoff = TimeSpan.Zero, MaxBackoff = TimeSpan.Zero };

    [Fact]
    public async Task ReceiveAsync_typed_deserializes_text_frames_until_close()
    {
        var socket = new FakeWebSocket()
            .QueueText("{\"value\":\"a\"}")
            .QueueText("{\"value\":\"b\"}")
            .QueueClose();
        var factory = FactoryFor(socket);

        await using var conn = await factory.ConnectAsync(Url);

        var values = new List<string>();
        await foreach (var dto in conn.ReceiveAsync<Dto>())
        {
            values.Add(dto.Value);
        }

        Assert.Equal(new[] { "a", "b" }, values);
    }

    [Fact]
    public async Task ReceiveAsync_raw_reassembles_continuation_frames()
    {
        var socket = new FakeWebSocket()
            .QueueText("part1", endOfMessage: false)
            .QueueText("part2", endOfMessage: true)
            .QueueClose(WebSocketCloseStatus.NormalClosure, "bye");
        var factory = FactoryFor(socket);

        await using var conn = await factory.ConnectAsync(Url);

        var messages = new List<WebSocketMessage>();
        await foreach (var message in conn.ReceiveAsync())
        {
            messages.Add(message);
        }

        Assert.Equal(2, messages.Count);
        var text = Assert.IsType<WebSocketTextMessage>(messages[0].Value);
        Assert.Equal("part1part2", text.Text);
        var close = Assert.IsType<WebSocketCloseMessage>(messages[1].Value);
        Assert.Equal("bye", close.Description);
    }

    [Fact]
    public async Task SendAsync_serializes_json_as_a_text_frame()
    {
        var socket = new FakeWebSocket();
        var factory = FactoryFor(socket);

        await using var conn = await factory.ConnectAsync(Url);
        await conn.SendAsync(new Dto { Value = "x" });

        var sent = Assert.Single(socket.Sent);
        Assert.Equal(WebSocketMessageType.Text, sent.Type);
        Assert.Contains("\"Value\":\"x\"", sent.Text);
    }

    [Fact]
    public async Task Fault_reconnects_runs_hook_and_resumes_delivery()
    {
        var first = new FakeWebSocket()
            .QueueText("{\"value\":\"a\"}")
            .QueueThrow(new WebSocketException("dropped"));
        var second = new FakeWebSocket()
            .QueueText("{\"value\":\"b\"}"); // then idle
        var factory = FactoryFor(first, second);

        var reconnected = false;
        var options = new WebSocketConnectionOptions
        {
            AutoReconnect = true,
            Reconnect = InstantReconnect,
            OnReconnected = (_, _) => { reconnected = true; return Task.CompletedTask; },
        };

        await using var conn = await factory.ConnectAsync(Url, options);

        await using var e = conn.ReceiveAsync<Dto>().GetAsyncEnumerator();
        Assert.True(await e.MoveNextAsync());
        Assert.Equal("a", e.Current.Value);
        Assert.True(await e.MoveNextAsync());
        Assert.Equal("b", e.Current.Value);

        Assert.True(reconnected);
        Assert.Equal(WebSocketConnectionState.Open, conn.State);
    }

    [Fact]
    public async Task Fault_without_autoreconnect_faults_the_stream_and_closes()
    {
        var socket = new FakeWebSocket().QueueThrow(new WebSocketException("dropped"));
        var factory = FactoryFor(socket);

        await using var conn = await factory.ConnectAsync(Url, new WebSocketConnectionOptions { AutoReconnect = false });

        await Assert.ThrowsAsync<WebSocketException>(async () =>
        {
            await foreach (var _ in conn.ReceiveAsync()) { }
        });
        Assert.Equal(WebSocketConnectionState.Closed, conn.State);
    }

    [Fact]
    public async Task CloseAsync_closes_the_socket_and_transitions_state()
    {
        var socket = new FakeWebSocket();
        var factory = FactoryFor(socket);

        var conn = await factory.ConnectAsync(Url);
        await conn.CloseAsync(WebSocketCloseStatus.NormalClosure, "done");

        Assert.Equal(WebSocketConnectionState.Closed, conn.State);
        Assert.Equal(WebSocketState.Closed, socket.State);

        await conn.DisposeAsync();
    }

    [Fact]
    public async Task StateChanged_fires_on_transition_to_open()
    {
        var socket = new FakeWebSocket();
        var factory = FactoryFor(socket);
        var observed = new List<WebSocketConnectionState>();

        var conn = await factory.ConnectAsync(Url);
        conn.StateChanged += observed.Add;
        await conn.CloseAsync();

        Assert.Contains(WebSocketConnectionState.Closed, observed);
        await conn.DisposeAsync();
    }
}
