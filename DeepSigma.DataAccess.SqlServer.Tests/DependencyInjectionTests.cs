using DeepSigma.DataAccess.Abstraction;
using DeepSigma.DataAccess.RelationalDatabase;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace DeepSigma.DataAccess.SqlServer.Tests;

/// <summary>
/// DI smoke tests. SqlServerConnectionFactory does not connect at construction, so these tests
/// work without a live SQL Server.
/// </summary>
public class DependencyInjectionTests
{
    private const string DummyConnectionString =
        "Server=localhost;Database=AppDb;Integrated Security=True;TrustServerCertificate=True;";

    [Fact]
    public void AddDeepSigmaSqlServer_resolves_IDbConnectionFactory_as_SqlServerConnectionFactory()
    {
        var services = new ServiceCollection();
        services.AddDeepSigmaSqlServer(DummyConnectionString);
        using var provider = services.BuildServiceProvider();

        var factory = provider.GetRequiredService<IDbConnectionFactory>();

        Assert.IsType<SqlServerConnectionFactory>(factory);
    }

    [Fact]
    public void AddDeepSigmaSqlServer_resolves_RelationalDatabaseApi()
    {
        var services = new ServiceCollection();
        services.AddDeepSigmaSqlServer(DummyConnectionString);
        using var provider = services.BuildServiceProvider();

        var api = provider.GetRequiredService<RelationalDatabaseApi>();

        Assert.NotNull(api);
    }

    [Fact]
    public void AddDeepSigmaSqlServer_resolves_IDatabaseSchemaService_as_SqlServerSchemaService()
    {
        var services = new ServiceCollection();
        services.AddDeepSigmaSqlServer(DummyConnectionString);
        using var provider = services.BuildServiceProvider();

        var schema = provider.GetRequiredService<IDatabaseSchemaService>();

        Assert.IsType<SqlServerSchemaService>(schema);
    }

    [Fact]
    public void AddDeepSigmaSqlServer_resolves_SqlServerBulkCopier()
    {
        var services = new ServiceCollection();
        services.AddDeepSigmaSqlServer(DummyConnectionString);
        using var provider = services.BuildServiceProvider();

        var bulk = provider.GetRequiredService<SqlServerBulkCopier>();

        Assert.NotNull(bulk);
    }

    [Fact]
    public void AddDeepSigmaSqlServer_resolves_MigrationRunner()
    {
        var services = new ServiceCollection();
        services.AddDeepSigmaSqlServer(DummyConnectionString);
        using var provider = services.BuildServiceProvider();

        var runner = provider.GetRequiredService<MigrationRunner>();

        Assert.NotNull(runner);
    }
}
