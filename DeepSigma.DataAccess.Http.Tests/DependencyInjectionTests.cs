using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace DeepSigma.DataAccess.Http.Tests;

public class DependencyInjectionTests
{
    [Fact]
    public void AddDeepSigmaHttp_resolves_HttpApi()
    {
        var services = new ServiceCollection();
        services.AddDeepSigmaHttp();

        using var provider = services.BuildServiceProvider();
        var http = provider.GetRequiredService<HttpApi>();

        Assert.NotNull(http);
    }

    [Fact]
    public void AddDeepSigmaHttp_with_configureClient_invokes_configuration()
    {
        var services = new ServiceCollection();
        services.AddDeepSigmaHttp(client => client.DefaultRequestHeaders.Add("X-Test", "value"));

        using var provider = services.BuildServiceProvider();
        var http = provider.GetRequiredService<HttpApi>();

        Assert.NotNull(http);
    }
}
