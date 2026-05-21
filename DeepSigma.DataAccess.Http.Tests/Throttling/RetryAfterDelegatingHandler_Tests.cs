using System.Net;
using System.Net.Http.Headers;
using DeepSigma.DataAccess.Http.Tests.Infrastructure;
using DeepSigma.DataAccess.Http.Throttling;
using Microsoft.Extensions.Time.Testing;
using Xunit;

namespace DeepSigma.DataAccess.Http.Tests.Throttling;

public class RetryAfterDelegatingHandler_Tests
{
    private static HttpClient BuildClient(RetryAfterOptions options, FakeTimeProvider clock, StubHttpMessageHandler inner)
    {
        var handler = new RetryAfterDelegatingHandler(options, clock) { InnerHandler = inner };
        return new HttpClient(handler);
    }

    private static StubHttpMessageHandler QueuedResponder(params HttpResponseMessage[] responses)
    {
        var queue = new Queue<HttpResponseMessage>(responses);
        return new StubHttpMessageHandler
        {
            Responder = (_, _) => Task.FromResult(queue.Count > 0
                ? queue.Dequeue()
                : new HttpResponseMessage(HttpStatusCode.OK)),
        };
    }

    private static HttpResponseMessage Status(HttpStatusCode status) => new(status);

    private static HttpResponseMessage Status(HttpStatusCode status, RetryConditionHeaderValue retryAfter)
    {
        var response = new HttpResponseMessage(status);
        response.Headers.RetryAfter = retryAfter;
        return response;
    }

    [Fact]
    public async Task NonRetryStatus_ReturnsImmediately()
    {
        var inner = QueuedResponder(Status(HttpStatusCode.OK));
        var clock = new FakeTimeProvider();
        var client = BuildClient(new RetryAfterOptions(), clock, inner);

        var response = await client.GetAsync("https://example.com", TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Single(inner.Requests);
    }

    [Fact]
    public async Task FailureThenSuccess_RetriesAndReturnsFinalResponse()
    {
        var inner = QueuedResponder(
            Status(HttpStatusCode.ServiceUnavailable, new RetryConditionHeaderValue(TimeSpan.FromSeconds(2))),
            Status(HttpStatusCode.OK));
        var clock = new FakeTimeProvider();
        var client = BuildClient(
            new RetryAfterOptions { MaxAttempts = 3 },
            clock, inner);

        var sendTask = client.GetAsync("https://example.com", TestContext.Current.CancellationToken);

        // Parked on the Retry-After delay.
        Assert.False(sendTask.IsCompleted);
        clock.Advance(TimeSpan.FromSeconds(2));

        var response = await sendTask;
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(2, inner.Requests.Count);
    }

    [Fact]
    public async Task RetryAfter_DeltaSeconds_IsHonored()
    {
        var inner = QueuedResponder(
            Status(HttpStatusCode.TooManyRequests, new RetryConditionHeaderValue(TimeSpan.FromSeconds(5))),
            Status(HttpStatusCode.OK));
        var clock = new FakeTimeProvider();
        var client = BuildClient(new RetryAfterOptions { MaxAttempts = 2 }, clock, inner);

        var sendTask = client.GetAsync("https://example.com", TestContext.Current.CancellationToken);

        // Under 5s: still waiting.
        clock.Advance(TimeSpan.FromSeconds(4));
        Assert.False(sendTask.IsCompleted);

        // Cross 5s: should fire.
        clock.Advance(TimeSpan.FromSeconds(1));
        var response = await sendTask;
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task RetryAfter_HttpDate_IsHonored()
    {
        var clock = new FakeTimeProvider(startDateTime: DateTimeOffset.UtcNow);
        DateTimeOffset retryAt = clock.GetUtcNow().AddSeconds(7);

        var inner = QueuedResponder(
            Status(HttpStatusCode.ServiceUnavailable, new RetryConditionHeaderValue(retryAt)),
            Status(HttpStatusCode.OK));
        var client = BuildClient(new RetryAfterOptions { MaxAttempts = 2 }, clock, inner);

        var sendTask = client.GetAsync("https://example.com", TestContext.Current.CancellationToken);

        clock.Advance(TimeSpan.FromSeconds(6));
        Assert.False(sendTask.IsCompleted);

        clock.Advance(TimeSpan.FromSeconds(2));
        var response = await sendTask;
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task RespectsMaxAttempts_ReturnsLastFailureResponse()
    {
        // Three failures with very short backoff to exercise the cap.
        var inner = QueuedResponder(
            Status(HttpStatusCode.ServiceUnavailable, new RetryConditionHeaderValue(TimeSpan.FromSeconds(1))),
            Status(HttpStatusCode.ServiceUnavailable, new RetryConditionHeaderValue(TimeSpan.FromSeconds(1))),
            Status(HttpStatusCode.ServiceUnavailable, new RetryConditionHeaderValue(TimeSpan.FromSeconds(1))));
        var clock = new FakeTimeProvider();
        var client = BuildClient(new RetryAfterOptions { MaxAttempts = 3 }, clock, inner);

        var sendTask = client.GetAsync("https://example.com", TestContext.Current.CancellationToken);

        // Two backoff delays of 1s each between the three attempts.
        clock.Advance(TimeSpan.FromSeconds(1));
        clock.Advance(TimeSpan.FromSeconds(1));

        var response = await sendTask;
        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
        Assert.Equal(3, inner.Requests.Count);
    }

    [Fact]
    public async Task NoRetryAfter_FallsBackToExponentialBackoff_BoundedByMaxBackoff()
    {
        var inner = QueuedResponder(
            Status(HttpStatusCode.ServiceUnavailable), // no Retry-After header
            Status(HttpStatusCode.OK));
        var clock = new FakeTimeProvider();
        var client = BuildClient(new RetryAfterOptions
        {
            MaxAttempts = 2,
            BaseBackoff = TimeSpan.FromMilliseconds(100),
            MaxBackoff = TimeSpan.FromSeconds(1),
        }, clock, inner);

        var sendTask = client.GetAsync("https://example.com", TestContext.Current.CancellationToken);

        // BaseBackoff + jitter is at most BaseBackoff*2; capped by MaxBackoff (1s). One advance past the cap is safe.
        clock.Advance(TimeSpan.FromSeconds(1));
        var response = await sendTask;

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(2, inner.Requests.Count);
    }

    [Fact]
    public async Task CustomRetryStatusCodes_AreUsed()
    {
        var inner = QueuedResponder(
            Status(HttpStatusCode.BadGateway), // not in default set
            Status(HttpStatusCode.OK));
        var clock = new FakeTimeProvider();
        var client = BuildClient(new RetryAfterOptions
        {
            MaxAttempts = 2,
            BaseBackoff = TimeSpan.FromMilliseconds(10),
            RetryStatusCodes = new HashSet<HttpStatusCode> { HttpStatusCode.BadGateway },
        }, clock, inner);

        var sendTask = client.GetAsync("https://example.com", TestContext.Current.CancellationToken);
        clock.Advance(TimeSpan.FromSeconds(1));
        var response = await sendTask;

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(2, inner.Requests.Count);
    }

    [Fact]
    public void Constructor_RejectsZeroMaxAttempts()
    {
        Assert.Throws<ArgumentException>(() =>
            new RetryAfterDelegatingHandler(new RetryAfterOptions { MaxAttempts = 0 }));
    }

    [Fact]
    public void Constructor_RejectsNullOptions()
    {
        Assert.Throws<ArgumentNullException>(() => new RetryAfterDelegatingHandler(null!));
    }
}
