using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace DeepSigma.DataAccess.MongoDB.Tests;

/// <summary>
/// DI smoke tests. MongoClient construction is lazy — it does not connect until the first
/// operation — so these tests work without a live MongoDB server.
/// </summary>
public class DependencyInjectionTests
{
    private const string DummyConnectionString = "mongodb://localhost:27017/";

    [Fact]
    public void AddDeepSigmaMongoDb_resolves_MongoDbApi()
    {
        var services = new ServiceCollection();
        services.AddDeepSigmaMongoDb(DummyConnectionString);
        using var provider = services.BuildServiceProvider();

        var api = provider.GetRequiredService<MongoDbApi>();

        Assert.NotNull(api);
    }

    [Fact]
    public void MongoDbApi_is_registered_as_singleton()
    {
        var services = new ServiceCollection();
        services.AddDeepSigmaMongoDb(DummyConnectionString);
        using var provider = services.BuildServiceProvider();

        var first = provider.GetRequiredService<MongoDbApi>();
        var second = provider.GetRequiredService<MongoDbApi>();

        Assert.Same(first, second);
    }
}
