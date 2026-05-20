using DeepSigma.DataAccess.Abstraction;
using DeepSigma.DataAccess.RelationalDatabase;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace DeepSigma.DataAccess.Postgres.Tests;

/// <summary>
/// DI smoke tests. PostgresConnectionFactory does not connect at construction, so these
/// tests work without a live PostgreSQL server.
/// </summary>
public class DependencyInjectionTests
{
    private const string DummyConnectionString = "Host=localhost;Database=appdb;Username=postgres;Password=postgres";

    [Fact]
    public void AddDeepSigmaPostgres_resolves_IDbConnectionFactory_as_PostgresConnectionFactory()
    {
        var services = new ServiceCollection();
        services.AddDeepSigmaPostgres(DummyConnectionString);
        using var provider = services.BuildServiceProvider();

        var factory = provider.GetRequiredService<IDbConnectionFactory>();

        Assert.IsType<PostgresConnectionFactory>(factory);
    }

    [Fact]
    public void AddDeepSigmaPostgres_resolves_RelationalDatabaseApi()
    {
        var services = new ServiceCollection();
        services.AddDeepSigmaPostgres(DummyConnectionString);
        using var provider = services.BuildServiceProvider();

        var api = provider.GetRequiredService<RelationalDatabaseApi>();

        Assert.NotNull(api);
    }

    [Fact]
    public void AddDeepSigmaPostgres_resolves_IDatabaseSchemaService_as_PostgresSchemaService()
    {
        var services = new ServiceCollection();
        services.AddDeepSigmaPostgres(DummyConnectionString);
        using var provider = services.BuildServiceProvider();

        var schema = provider.GetRequiredService<IDatabaseSchemaService>();

        Assert.IsType<PostgresSchemaService>(schema);
    }

    [Fact]
    public void AddDeepSigmaPostgres_resolves_PostgresBulkCopier()
    {
        var services = new ServiceCollection();
        services.AddDeepSigmaPostgres(DummyConnectionString);
        using var provider = services.BuildServiceProvider();

        var bulk = provider.GetRequiredService<PostgresBulkCopier>();

        Assert.NotNull(bulk);
    }
}
