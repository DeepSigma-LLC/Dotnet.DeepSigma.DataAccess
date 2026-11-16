using System.Net;
using System.Net.Http.Headers;
using DeepSigma.DataAccess.Web.Tests.Infrastructure;
using DeepSigma.DataAccess.Web.Throttling;
using Microsoft.Extensions.Time.Testing;
using Xunit;

namespace DeepSigma.DataAccess.Web.Tests.Throttling;

/// <summary>The handlers composed without dependency injection (<see cref="HttpPipeline"/>), and the one reading of
/// <c>Retry-After</c> (<see cref="RetryAfterHeader"/>).</summary>
public class HttpPipeline_Tests
{
    /// <summary>A handler that answers 429 then 200, and records whether it was disposed.</summary>
    private sealed class Answers(params HttpStatusCode[] statuses) : HttpMessageHandler
    {
        private readonly Queue<HttpStatusCode> _statuses = new(statuses);
        public int Sent { get; private set; }
        public bool Disposed { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Sent++;
            var response = new HttpResponseMessage(_statuses.Count > 0 ? _statuses.Dequeue() : HttpStatusCode.OK);
            response.Headers.RetryAfter = new RetryConditionHeaderValue(TimeSpan.FromMilliseconds(1));
            return Task.FromResult(response);
        }

        protected override void Dispose(bool disposing) => Disposed = true;
    }

    [Fact]
    public async Task The_client_retries_and_takes_its_address_and_timeout()
    {
        var inner = new Answers(HttpStatusCode.TooManyRequests);
        using var client = HttpPipeline.CreateClient(inner, new HttpPipelineOptions
        {
            Retry = new RetryAfterOptions { MaxAttempts = 3 }, BaseAddress = new Uri("https://example.com/"), Timeout = Timeout.InfiniteTimeSpan,
        });
        using var response = await client.GetAsync("v1/thing", TestContext.Current.CancellationToken);

        Assert.Equal((HttpStatusCode.OK, 2), (response.StatusCode, inner.Sent));
        Assert.Equal((new Uri("https://example.com/"), Timeout.InfiniteTimeSpan), (client.BaseAddress, client.Timeout));
    }

    [Fact]
    public async Task Without_options_the_handler_alone_sends()
    {
        var inner = new Answers(HttpStatusCode.TooManyRequests);
        using var client = HttpPipeline.CreateClient(inner);
        using var response = await client.GetAsync("https://example.com/", TestContext.Current.CancellationToken);
        Assert.Equal((HttpStatusCode.TooManyRequests, 1), (response.StatusCode, inner.Sent));
    }

    [Fact]
    public async Task Every_attempt_is_spaced_retries_included()
    {
        var clock = new FakeTimeProvider();
        var inner = new Answers(HttpStatusCode.ServiceUnavailable);
        using var client = HttpPipeline.CreateClient(inner, new HttpPipelineOptions
        {
            Retry = new RetryAfterOptions { MaxAttempts = 2 },
            Throttle = new MinIntervalOptions { Key = $"pipeline-test-{Guid.NewGuid()}", MinInterval = TimeSpan.FromSeconds(10) },
            Clock = clock,
        });
        var sending = client.GetAsync("https://example.com/", TestContext.Current.CancellationToken);
        for (var step = 0; step < 20 && !sending.IsCompleted; step++)
        {
            await Task.Delay(10, TestContext.Current.CancellationToken);
            clock.Advance(TimeSpan.FromSeconds(1));
        }
        using var response = await sending;
        Assert.Equal((HttpStatusCode.OK, 2), (response.StatusCode, inner.Sent));
        Assert.True(clock.GetUtcNow() - clock.Start >= TimeSpan.FromSeconds(10));                 // the retry waited for its turn
    }

    [Fact]
    public void Disposing_the_client_disposes_the_handler_only_when_it_owns_it()
    {
        var owned = new Answers();
        HttpPipeline.CreateClient(owned, new HttpPipelineOptions { Retry = new RetryAfterOptions() }).Dispose();
        Assert.True(owned.Disposed);

        var shared = new Answers();
        HttpPipeline.CreateClient(shared, new HttpPipelineOptions { Retry = new RetryAfterOptions() }, disposeHandler: false).Dispose();
        HttpPipeline.CreateClient(shared, disposeHandler: false).Dispose();
        Assert.False(shared.Disposed);
    }

    [Fact]
    public void Retry_After_is_read_as_seconds_or_as_a_date_from_now()
    {
        var clock = new FakeTimeProvider(new DateTimeOffset(2026, 11, 15, 12, 0, 0, TimeSpan.Zero));
        HttpResponseMessage With(RetryConditionHeaderValue? value)
        {
            var response = new HttpResponseMessage(HttpStatusCode.TooManyRequests);
            response.Headers.RetryAfter = value;
            return response;
        }

        Assert.Equal(TimeSpan.FromSeconds(30), RetryAfterHeader.GetDelay(With(new RetryConditionHeaderValue(TimeSpan.FromSeconds(30))), clock));
        Assert.Equal(TimeSpan.FromMinutes(2), RetryAfterHeader.GetDelay(With(new RetryConditionHeaderValue(clock.GetUtcNow().AddMinutes(2))), clock));
        Assert.Null(RetryAfterHeader.GetDelay(With(new RetryConditionHeaderValue(clock.GetUtcNow().AddMinutes(-1))), clock));   // already past
        Assert.Null(RetryAfterHeader.GetDelay(With(new RetryConditionHeaderValue(TimeSpan.Zero)), clock));
        Assert.Null(RetryAfterHeader.GetDelay(With(null), clock));
    }
}
