using DeepSigma.DataAccess.Abstraction;
using DeepSigma.DataAccess.RelationalDatabase;
using DeepSigma.DataAccess.Sqlite.Tests.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace DeepSigma.DataAccess.Sqlite.Tests;

public class DependencyInjectionTests : IClassFixture<SqliteSharedMemoryFixture>
{
    private readonly SqliteSharedMemoryFixture _fixture;

    public DependencyInjectionTests(SqliteSharedMemoryFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public void AddDeepSigmaSqlite_resolves_IDbConnectionFactory()
    {
        var services = new ServiceCollection();
        services.AddDeepSigmaSqlite(_fixture.ConnectionString);
        using var provider = services.BuildServiceProvider();

        var factory = provider.GetRequiredService<IDbConnectionFactory>();

        Assert.IsType<SqliteConnectionFactory>(factory);
    }

    [Fact]
    public void AddDeepSigmaSqlite_resolves_RelationalDatabaseApi()
    {
        var services = new ServiceCollection();
        services.AddDeepSigmaSqlite(_fixture.ConnectionString);
        using var provider = services.BuildServiceProvider();

        var api = provider.GetRequiredService<RelationalDatabaseApi>();

        Assert.NotNull(api);
    }

    [Fact]
    public void AddDeepSigmaSqlite_resolves_IDatabaseSchemaService()
    {
        var services = new ServiceCollection();
        services.AddDeepSigmaSqlite(_fixture.ConnectionString);
        using var provider = services.BuildServiceProvider();

        var schema = provider.GetRequiredService<IDatabaseSchemaService>();

        Assert.IsType<SqliteSchemaService>(schema);
    }
}
