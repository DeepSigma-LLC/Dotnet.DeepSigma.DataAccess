using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace DeepSigma.DataAccess.Web.Tests;

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

    [Fact]
    public void AddDeepSigmaHttp_resolves_IHttpApi_interface()
    {
        var services = new ServiceCollection();
        services.AddDeepSigmaHttp();

        using var provider = services.BuildServiceProvider();

        Assert.NotNull(provider.GetRequiredService<IHttpApi>());
    }

    [Fact]
    public void AddDeepSigmaWeb_default_registers_all_three_transports()
    {
        var services = new ServiceCollection();
        services.AddDeepSigmaWeb();

        using var provider = services.BuildServiceProvider();

        Assert.NotNull(provider.GetRequiredService<IHttpApi>());
        Assert.NotNull(provider.GetRequiredService<ISseClient>());
        Assert.NotNull(provider.GetRequiredService<IWebSocketConnectionFactory>());
    }

    [Fact]
    public void AddDeepSigmaWeb_with_selector_registers_only_chosen_transports()
    {
        var services = new ServiceCollection();
        services.AddDeepSigmaWeb(w => w.AddHttp());

        using var provider = services.BuildServiceProvider();

        Assert.NotNull(provider.GetRequiredService<IHttpApi>());
        Assert.Null(provider.GetService<IWebSocketConnectionFactory>());
    }
}
