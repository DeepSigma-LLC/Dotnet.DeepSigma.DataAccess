using DeepSigma.DataAccess.Sqlite.Tests.Infrastructure;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace DeepSigma.DataAccess.Sqlite.Tests;

public class SqliteHealthCheckTests : IClassFixture<SqliteSharedMemoryFixture>
{
    private readonly SqliteSharedMemoryFixture _fixture;

    public SqliteHealthCheckTests(SqliteSharedMemoryFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task Reports_Healthy_when_database_is_reachable()
    {
        var services = new ServiceCollection();
        services.AddHealthChecks().AddDeepSigmaSqlite(_fixture.ConnectionString);

        using var provider = services.BuildServiceProvider();
        var healthService = provider.GetRequiredService<HealthCheckService>();
        var report = await healthService.CheckHealthAsync();

        Assert.Equal(HealthStatus.Healthy, report.Status);
        Assert.Contains(report.Entries, e => e.Key == "deepsigma_sqlite" && e.Value.Status == HealthStatus.Healthy);
    }

    [Fact]
    public async Task Reports_Unhealthy_when_connection_string_is_invalid()
    {
        // SQLite is permissive about file paths but rejects unparseable connection strings
        const string badConnectionString = "garbage=does;not;parse";

        var services = new ServiceCollection();
        services.AddHealthChecks().AddDeepSigmaSqlite(badConnectionString);

        using var provider = services.BuildServiceProvider();
        var healthService = provider.GetRequiredService<HealthCheckService>();
        var report = await healthService.CheckHealthAsync();

        Assert.Equal(HealthStatus.Unhealthy, report.Status);
    }
}
