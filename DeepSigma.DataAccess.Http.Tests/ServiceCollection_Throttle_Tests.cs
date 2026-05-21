using System.Net;
using DeepSigma.DataAccess.Http.Tests.Infrastructure;
using DeepSigma.DataAccess.Http.Throttling;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace DeepSigma.DataAccess.Http.Tests;

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
