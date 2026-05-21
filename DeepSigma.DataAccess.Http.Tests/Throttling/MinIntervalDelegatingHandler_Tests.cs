using System.Net;
using DeepSigma.DataAccess.Http.Tests.Infrastructure;
using DeepSigma.DataAccess.Http.Throttling;
using Microsoft.Extensions.Time.Testing;
using Xunit;

namespace DeepSigma.DataAccess.Http.Tests.Throttling;

public class MinIntervalDelegatingHandler_Tests
{
    private static (HttpClient client, FakeTimeProvider clock, StubHttpMessageHandler inner, List<DateTimeOffset> sentAt) Build(
        TimeSpan minInterval, string key, FakeTimeProvider? clock = null)
    {
        clock ??= new FakeTimeProvider(startDateTime: DateTimeOffset.UtcNow);
        var clockRef = clock;
        var sentAt = new List<DateTimeOffset>();

        var inner = new StubHttpMessageHandler
        {
            Responder = (_, _) =>
            {
                sentAt.Add(clockRef.GetUtcNow());
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK));
            },
        };

        var handler = new MinIntervalDelegatingHandler(
            new MinIntervalOptions { MinInterval = minInterval, Key = key },
            clock)
        { InnerHandler = inner };

        return (new HttpClient(handler), clock, inner, sentAt);
    }

    [Fact]
    public async Task FirstRequest_IsNotDelayed()
    {
        var (client, clock, _, sentAt) = Build(TimeSpan.FromSeconds(3), key: $"k-{Guid.NewGuid()}");
        DateTimeOffset start = clock.GetUtcNow();

        await client.GetAsync("https://example.com/a", TestContext.Current.CancellationToken);

        Assert.Single(sentAt);
        Assert.Equal(start, sentAt[0]);
    }

    [Fact]
    public async Task SecondRequest_WaitsForMinInterval()
    {
        var (client, clock, _, sentAt) = Build(TimeSpan.FromSeconds(3), key: $"k-{Guid.NewGuid()}");

        await client.GetAsync("https://example.com/a", TestContext.Current.CancellationToken);

        // Kick off second request — it should park on the throttle delay.
        var second = client.GetAsync("https://example.com/b", TestContext.Current.CancellationToken);
        Assert.False(second.IsCompleted);

        // Advance under the interval; still waiting.
        clock.Advance(TimeSpan.FromSeconds(1));
        Assert.False(second.IsCompleted);

        // Cross the threshold; should release.
        clock.Advance(TimeSpan.FromSeconds(2));
        await second;

        Assert.Equal(2, sentAt.Count);
        Assert.Equal(TimeSpan.FromSeconds(3), sentAt[1] - sentAt[0]);
    }

    [Fact]
    public async Task NoWait_WhenIntervalAlreadyElapsedNaturally()
    {
        var (client, clock, _, sentAt) = Build(TimeSpan.FromSeconds(3), key: $"k-{Guid.NewGuid()}");

        await client.GetAsync("https://example.com/a", TestContext.Current.CancellationToken);

        // Caller naturally waits 5s before making the next call.
        clock.Advance(TimeSpan.FromSeconds(5));

        DateTimeOffset before = clock.GetUtcNow();
        await client.GetAsync("https://example.com/b", TestContext.Current.CancellationToken);

        // Second request should fire immediately, no additional delay.
        Assert.Equal(before, sentAt[1]);
    }

    [Fact]
    public async Task DifferentKeys_DoNotInterfere()
    {
        // Share a single clock so the two clients see the same time.
        var sharedClock = new FakeTimeProvider(startDateTime: DateTimeOffset.UtcNow);
        string keyA = $"k-a-{Guid.NewGuid()}";
        string keyB = $"k-b-{Guid.NewGuid()}";
        var (clientA, _, _, sentA) = Build(TimeSpan.FromSeconds(3), key: keyA, clock: sharedClock);
        var (clientB, _, _, sentB) = Build(TimeSpan.FromSeconds(3), key: keyB, clock: sharedClock);

        await clientA.GetAsync("https://example.com/a", TestContext.Current.CancellationToken);

        DateTimeOffset beforeB = sharedClock.GetUtcNow();
        // Different key → not throttled by client A's recent send.
        await clientB.GetAsync("https://example.com/b", TestContext.Current.CancellationToken);

        Assert.Single(sentA);
        Assert.Single(sentB);
        Assert.Equal(beforeB, sentB[0]);
    }

    [Fact]
    public async Task SameKey_AcrossClients_CooperatesOnSpacing()
    {
        // Two MinIntervalDelegatingHandlers using the same key must share state.
        // Share a single clock so Task.Delay on either handler reacts to the same Advance().
        var sharedClock = new FakeTimeProvider(startDateTime: DateTimeOffset.UtcNow);
        string sharedKey = $"shared-{Guid.NewGuid()}";
        var (clientA, _, _, sentA) = Build(TimeSpan.FromSeconds(3), key: sharedKey, clock: sharedClock);
        var (clientB, _, _, sentB) = Build(TimeSpan.FromSeconds(3), key: sharedKey, clock: sharedClock);

        await clientA.GetAsync("https://example.com/a", TestContext.Current.CancellationToken);

        var taskB = clientB.GetAsync("https://example.com/b", TestContext.Current.CancellationToken);
        Assert.False(taskB.IsCompleted);

        sharedClock.Advance(TimeSpan.FromSeconds(3));
        await taskB;

        Assert.Single(sentA);
        Assert.Single(sentB);
        Assert.Equal(TimeSpan.FromSeconds(3), sentB[0] - sentA[0]);
    }

    [Fact]
    public void Constructor_RejectsEmptyKey()
    {
        Assert.Throws<ArgumentException>(() =>
            new MinIntervalDelegatingHandler(new MinIntervalOptions { Key = "" }));
    }

    [Fact]
    public void Constructor_RejectsNullOptions()
    {
        Assert.Throws<ArgumentNullException>(() => new MinIntervalDelegatingHandler(null!));
    }
}
