using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace DeepSigma.DataAccess.Cosmos.Tests;

/// <summary>
/// DI smoke tests. CosmosClient construction is lazy — it does not connect until the first
/// operation — so these tests work without a live Cosmos endpoint or emulator.
/// </summary>
public class DependencyInjectionTests
{
    private const string DummyEndpoint = "https://localhost:8081/";
    private const string DummyKey = "C2y6yDjf5/R+ob0N8A7Cgv30VRDJIWEHLM+4QDU5DE2nQ9nDuVTqobD4b8mGGyPMbIZnqyMsEcaGQy67XIw/Jw==";
    private const string AppName = "deepsigma-tests";

    [Fact]
    public void AddDeepSigmaCosmos_resolves_CosmosDbApi()
    {
        var services = new ServiceCollection();
        services.AddDeepSigmaCosmos(DummyEndpoint, DummyKey, AppName);
        using var provider = services.BuildServiceProvider();

        var api = provider.GetRequiredService<CosmosDbApi>();

        Assert.NotNull(api);
    }

    [Fact]
    public void CosmosDbApi_is_registered_as_singleton()
    {
        var services = new ServiceCollection();
        services.AddDeepSigmaCosmos(DummyEndpoint, DummyKey, AppName);
        using var provider = services.BuildServiceProvider();

        var first = provider.GetRequiredService<CosmosDbApi>();
        var second = provider.GetRequiredService<CosmosDbApi>();

        Assert.Same(first, second);
    }
}
