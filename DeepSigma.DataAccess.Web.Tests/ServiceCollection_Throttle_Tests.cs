using System.Net;
using DeepSigma.DataAccess.Web.Tests.Infrastructure;
using DeepSigma.DataAccess.Web.Throttling;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace DeepSigma.DataAccess.Web.Tests;

public class ServiceCollection_Throttle_Tests
{
    [Fact]
    public void AddMinIntervalThrottle_BuildsClient_WithoutErrors()
    {
        var services = new ServiceCollection();
        services.AddHttpClient("test").AddMinIntervalThrottle(TimeSpan.FromMilliseconds(50));

        using var provider = services.BuildServiceProvider();
        var factory = provider.GetRequiredService<IHttpClientFactory>();
        using HttpClient client = factory.CreateClient("test");

        Assert.NotNull(client);
    }

    [Fact]
    public void AddRetryAfterPolicy_BuildsClient_WithoutErrors()
    {
        var services = new ServiceCollection();
        services.AddHttpClient("test").AddRetryAfterPolicy(o => o.MaxAttempts = 3);

        using var provider = services.BuildServiceProvider();
        var factory = provider.GetRequiredService<IHttpClientFactory>();
        using HttpClient client = factory.CreateClient("test");

        Assert.NotNull(client);
    }

    [Fact]
    public void Chained_Throttle_And_Retry_BuildsClient()
    {
        var services = new ServiceCollection();
        services.AddHttpClient("test")
                .AddMinIntervalThrottle(TimeSpan.FromMilliseconds(10), key: "test-key")
                .AddRetryAfterPolicy();

        using var provider = services.BuildServiceProvider();
        var factory = provider.GetRequiredService<IHttpClientFactory>();
        using HttpClient client = factory.CreateClient("test");

        Assert.NotNull(client);
    }

    /// <summary>The first handler added is the outermost: retry added first resubmits through the throttle, so its
    /// attempts are paced; throttle added first leaves the retry inside it, unpaced (README, Composition).</summary>
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Retries_are_paced_only_when_the_retry_is_added_first(bool retryFirst)
    {
        var responses = new Queue<HttpStatusCode>([HttpStatusCode.ServiceUnavailable, HttpStatusCode.OK]);
        var stub = new StubHttpMessageHandler
        {
            Responder = (_, _) =>
            {
                var response = new HttpResponseMessage(responses.Count > 0 ? responses.Dequeue() : HttpStatusCode.OK);
                response.Headers.RetryAfter = new System.Net.Http.Headers.RetryConditionHeaderValue(TimeSpan.FromMilliseconds(1));
                return Task.FromResult(response);
            },
        };
        var services = new ServiceCollection();
        var builder = services.AddHttpClient("order").ConfigurePrimaryHttpMessageHandler(() => stub);
        var key = $"order-{Guid.NewGuid()}";
        if (retryFirst) builder.AddRetryAfterPolicy(o => o.MaxAttempts = 2).AddMinIntervalThrottle(TimeSpan.FromMilliseconds(600), key);
        else builder.AddMinIntervalThrottle(TimeSpan.FromMilliseconds(600), key).AddRetryAfterPolicy(o => o.MaxAttempts = 2);

        using var provider = services.BuildServiceProvider();
        using HttpClient client = provider.GetRequiredService<IHttpClientFactory>().CreateClient("order");
        var timer = System.Diagnostics.Stopwatch.StartNew();
        using var response = await client.GetAsync("https://example.com/", TestContext.Current.CancellationToken);

        Assert.Equal((HttpStatusCode.OK, 2), (response.StatusCode, stub.Requests.Count));
        if (retryFirst) Assert.True(timer.Elapsed >= TimeSpan.FromMilliseconds(550), $"the retry was not paced ({timer.Elapsed})");
        else Assert.True(timer.Elapsed < TimeSpan.FromMilliseconds(450), $"the retry was paced ({timer.Elapsed})");
    }

    [Fact]
    public async Task RetryAfter_EndToEnd_RetriesOn503()
    {
        // Verify the handler is actually wired into the pipeline by inducing a retry.
        var responses = new Queue<HttpResponseMessage>(new[]
        {
            new HttpResponseMessage(HttpStatusCode.ServiceUnavailable),
            new HttpResponseMessage(HttpStatusCode.OK),
        });
        var stub = new StubHttpMessageHandler
        {
            Responder = (_, _) => Task.FromResult(responses.Count > 0
                ? responses.Dequeue()
                : new HttpResponseMessage(HttpStatusCode.OK)),
        };

        var services = new ServiceCollection();
        services.AddHttpClient("test")
                .AddRetryAfterPolicy(o =>
                {
                    o.MaxAttempts = 2;
                    o.BaseBackoff = TimeSpan.FromMilliseconds(1);
                    o.MaxBackoff = TimeSpan.FromMilliseconds(10);
                })
                .ConfigurePrimaryHttpMessageHandler(() => stub);

        using var provider = services.BuildServiceProvider();
        var factory = provider.GetRequiredService<IHttpClientFactory>();
        using HttpClient client = factory.CreateClient("test");

        var response = await client.GetAsync("https://example.com", TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(2, stub.Requests.Count);
    }
}
