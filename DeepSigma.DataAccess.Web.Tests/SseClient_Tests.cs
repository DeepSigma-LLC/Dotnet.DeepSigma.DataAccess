using System.Net;
using System.Net.Http.Headers;
using System.Net.ServerSentEvents;
using System.Text;
using DeepSigma.DataAccess.Web.Resilience;
using DeepSigma.DataAccess.Web.Tests.Infrastructure;
using Microsoft.Extensions.Time.Testing;
using Xunit;

// These tests drive subscription lifetime explicitly (their own CancellationTokenSource and a
// FakeTimeProvider), so the analyzer's suggestion to thread TestContext's token does not apply.
#pragma warning disable xUnit1051

namespace DeepSigma.DataAccess.Web.Tests;

public class SseClient_Tests
{
    private const string Url = "http://localhost/stream";

    private sealed record Dto
    {
        public string Value { get; init; } = "";
    }

    private static HttpResponseMessage EventStream(string body)
    {
        var content = new StringContent(body, Encoding.UTF8);
        content.Headers.ContentType = new MediaTypeHeaderValue("text/event-stream");
        return new HttpResponseMessage(HttpStatusCode.OK) { Content = content };
    }

    private static SseClient NewClient(StubHttpMessageHandler handler, TimeProvider? clock = null)
        => new(new HttpClient(handler), timeProvider: clock);

    [Fact]
    public async Task SubscribeAsync_raw_yields_each_event_with_type_and_id()
    {
        var handler = new StubHttpMessageHandler
        {
            Responder = (_, _) => Task.FromResult(EventStream(
                "id: 1\nevent: greeting\ndata: hello\n\n" +
                "id: 2\nevent: greeting\ndata: world\n\n")),
        };
        var client = NewClient(handler);

        var items = new List<SseItem<string>>();
        await foreach (var item in client.SubscribeAsync(Url, new SseSubscribeOptions { AutoReconnect = false }))
        {
            items.Add(item);
        }

        Assert.Equal(2, items.Count);
        Assert.Equal("hello", items[0].Data);
        Assert.Equal("greeting", items[0].EventType);
        Assert.Equal("1", items[0].EventId);
        Assert.Equal("world", items[1].Data);
        Assert.Single(handler.Requests);
        Assert.Equal("text/event-stream", handler.Requests[0].Headers.Accept.Single().MediaType);
    }

    [Fact]
    public async Task SubscribeAsync_typed_deserializes_json_data()
    {
        var handler = new StubHttpMessageHandler
        {
            Responder = (_, _) => Task.FromResult(EventStream(
                "data: {\"value\":\"a\"}\n\n" +
                "data: {\"value\":\"b\"}\n\n")),
        };
        var client = NewClient(handler);

        var items = new List<Dto>();
        await foreach (var dto in client.SubscribeAsync<Dto>(Url, new SseSubscribeOptions { AutoReconnect = false }))
        {
            items.Add(dto);
        }

        Assert.Equal(new[] { "a", "b" }, items.Select(d => d.Value));
    }

    [Fact]
    public async Task EventTypeFilter_only_yields_matching_events()
    {
        var handler = new StubHttpMessageHandler
        {
            Responder = (_, _) => Task.FromResult(EventStream(
                "event: price\ndata: p1\n\n" +
                "event: heartbeat\ndata: h\n\n" +
                "event: price\ndata: p2\n\n")),
        };
        var client = NewClient(handler);

        var data = new List<string>();
        await foreach (var item in client.SubscribeAsync(Url, new SseSubscribeOptions { AutoReconnect = false, EventTypeFilter = "price" }))
        {
            data.Add(item.Data);
        }

        Assert.Equal(new[] { "p1", "p2" }, data);
    }

    [Fact]
    public async Task AutoReconnect_false_connects_once_and_completes_on_stream_end()
    {
        var handler = new StubHttpMessageHandler
        {
            Responder = (_, _) => Task.FromResult(EventStream("data: only\n\n")),
        };
        var client = NewClient(handler);

        var count = 0;
        await foreach (var _ in client.SubscribeAsync(Url, new SseSubscribeOptions { AutoReconnect = false }))
        {
            count++;
        }

        Assert.Equal(1, count);
        Assert.Single(handler.Requests);
    }

    [Fact]
    public async Task Reconnect_waits_retry_hint_then_resumes_with_last_event_id()
    {
        var clock = new FakeTimeProvider();
        var handler = new StubHttpMessageHandler();
        var call = 0;
        handler.Responder = (_, _) =>
        {
            call++;
            // First stream sets a 100s retry hint and event id 1, then closes cleanly.
            // Subsequent streams deliver the next event.
            string body = call == 1
                ? "retry: 100000\nid: 1\nevent: price\ndata: a\n\n"
                : "id: 2\nevent: price\ndata: b\n\n";
            return Task.FromResult(EventStream(body));
        };
        var client = NewClient(handler, clock);

        using var cts = new CancellationTokenSource();
        await using var e = client
            .SubscribeAsync(Url, new SseSubscribeOptions { AutoReconnect = true }, cts.Token)
            .GetAsyncEnumerator();

        Assert.True(await e.MoveNextAsync());
        Assert.Equal("a", e.Current.Data);

        // The first stream has ended; the next read parks on the 100s reconnect delay.
        var move2 = e.MoveNextAsync();
        Assert.False(move2.IsCompleted);

        clock.Advance(TimeSpan.FromSeconds(100));

        Assert.True(await move2);
        Assert.Equal("b", e.Current.Data);

        // The reconnect must have resumed from the last event id.
        Assert.Equal("1", handler.Requests[1].Headers.GetValues("Last-Event-ID").Single());

        await cts.CancelAsync();
    }

    [Fact]
    public async Task Connect_failure_is_retried_up_to_the_policy_budget_then_surfaces()
    {
        var handler = new StubHttpMessageHandler
        {
            Responder = (_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.InternalServerError)),
        };
        var client = NewClient(handler);

        var options = new SseSubscribeOptions
        {
            AutoReconnect = true,
            // Zero backoff keeps the test instant and clock-free; we only assert the attempt budget.
            Reconnect = new ReconnectPolicy { MaxAttempts = 3, BaseBackoff = TimeSpan.Zero, MaxBackoff = TimeSpan.Zero },
        };

        await Assert.ThrowsAsync<HttpRequestException>(async () =>
        {
            await foreach (var _ in client.SubscribeAsync(Url, options)) { }
        });
        Assert.Equal(3, handler.Requests.Count);
    }
}
