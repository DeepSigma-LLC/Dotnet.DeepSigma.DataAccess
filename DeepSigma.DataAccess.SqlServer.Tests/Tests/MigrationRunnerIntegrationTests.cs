using DeepSigma.DataAccess.Abstraction;
using DeepSigma.DataAccess.RelationalDatabase;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace DeepSigma.DataAccess.SqlServer.Tests.Tests;

/// <summary>
/// Verifies that the SQL Server-flavoured <c>_migrations</c> DDL registered by
/// <c>AddDeepSigmaSqlServer</c> actually parses and that <see cref="MigrationRunner"/> applies
/// real DDL through Microsoft.Data.SqlClient end-to-end.
/// </summary>
/// <remarks>
/// Requires a running SQL Server instance reachable at the connection string below. Override the
/// <c>DEEPSIGMA_SQLSERVER_CONNECTION</c> environment variable to point at your own server.
/// Each test creates uniquely-named tables (Guid-suffixed) and cleans them up — and its tracking
/// rows — on dispose, so reruns and parallel runs against the same database are safe.
/// </remarks>
[Trait("Category", "Integration")]
public class MigrationRunnerIntegrationTests : IAsyncDisposable
{
    private static readonly string ConnectionString =
        Environment.GetEnvironmentVariable("DEEPSIGMA_SQLSERVER_CONNECTION")
        ?? "Data Source=localhost;Database=AutoML;Integrated Security=True;Persist Security Info=False;Pooling=False;MultipleActiveResultSets=False;Connect Timeout=30;Encrypt=False;TrustServerCertificate=True;Packet Size=4096;Command Timeout=0;";

    private readonly string _suffix = Guid.NewGuid().ToString("N");
    private readonly ServiceProvider _provider;
    private readonly MigrationRunner _runner;
    private readonly RelationalDatabaseApi _db;

    private string T(string name) => $"deepsigma_mr_{name}_{_suffix}";
    private string Id(string name) => $"{_suffix}_{name}";

    public MigrationRunnerIntegrationTests()
    {
        var services = new ServiceCollection();
        services.AddDeepSigmaSqlServer(ConnectionString);
        _provider = services.BuildServiceProvider();
        _runner = _provider.GetRequiredService<MigrationRunner>();
        _db = _provider.GetRequiredService<RelationalDatabaseApi>();
    }

    public async ValueTask DisposeAsync()
    {
        try
        {
            await _db.ExecuteAsync($"IF OBJECT_ID(N'{T("widgets")}', N'U') IS NOT NULL DROP TABLE {T("widgets")};");
            await _db.ExecuteAsync($"IF OBJECT_ID(N'{T("gadgets")}', N'U') IS NOT NULL DROP TABLE {T("gadgets")};");
            await _db.ExecuteAsync(
                "DELETE FROM _migrations WHERE Id LIKE @Prefix",
                new { Prefix = $"{_suffix}_%" });
        }
        catch
        {
            // Cleanup is best-effort.
        }
        await _provider.DisposeAsync();
    }

    [Fact]
    public async Task Creates_tracking_table_and_applies_a_migration()
    {
        var ct = TestContext.Current.CancellationToken;
        Migration[] migrations =
        [
            new(Id("001"), $"CREATE TABLE {T("widgets")} (Id INT IDENTITY(1,1) PRIMARY KEY, Name NVARCHAR(100) NOT NULL);", "create widgets"),
        ];

        IReadOnlyList<string> applied = await _runner.ApplyAsync(migrations, ct);

        Assert.Equal(new[] { Id("001") }, applied);

        await _db.ExecuteAsync(
            $"INSERT INTO {T("widgets")} (Name) VALUES (@Name)",
            new { Name = "w" }, cancellationToken: ct);
        long? count = await _db.ExecuteScalarAsync<long?>(
            $"SELECT COUNT_BIG(*) FROM {T("widgets")}", cancellationToken: ct);
        Assert.Equal(1, count);
    }

    [Fact]
    public async Task Tracking_table_is_idempotent_across_runs()
    {
        var ct = TestContext.Current.CancellationToken;
        Migration[] first =
        [
            new(Id("001"), $"CREATE TABLE {T("widgets")} (Id INT IDENTITY(1,1) PRIMARY KEY);"),
        ];
        await _runner.ApplyAsync(first, ct);

        Migration[] second =
        [
            new(Id("001"), $"CREATE TABLE {T("widgets")} (Id INT IDENTITY(1,1) PRIMARY KEY);"),
            new(Id("002"), $"CREATE TABLE {T("gadgets")} (Id INT IDENTITY(1,1) PRIMARY KEY);"),
        ];
        IReadOnlyList<string> applied = await _runner.ApplyAsync(second, ct);

        Assert.Equal(new[] { Id("002") }, applied);

        long? widgets = await _db.ExecuteScalarAsync<long?>(
            $"SELECT COUNT_BIG(*) FROM sys.objects WHERE type = 'U' AND name = '{T("widgets")}'",
            cancellationToken: ct);
        long? gadgets = await _db.ExecuteScalarAsync<long?>(
            $"SELECT COUNT_BIG(*) FROM sys.objects WHERE type = 'U' AND name = '{T("gadgets")}'",
            cancellationToken: ct);
        Assert.Equal(1, widgets);
        Assert.Equal(1, gadgets);
    }

    [Fact]
    public async Task Failing_migration_rolls_back_and_leaves_no_tracking_row()
    {
        var ct = TestContext.Current.CancellationToken;
        Migration[] migrations =
        [
            new(Id("001"), $"CREATE TABLE {T("widgets")} (Id INT IDENTITY(1,1) PRIMARY KEY);"),
            new(Id("002"), "THIS IS NOT VALID SQL;"),
        ];

        await Assert.ThrowsAnyAsync<Exception>(() => _runner.ApplyAsync(migrations, ct));

        IEnumerable<string> recordedIds = await _db.GetAllAsync<object, string>(
            "SELECT Id FROM _migrations WHERE Id LIKE @Prefix ORDER BY Id",
            new { Prefix = $"{_suffix}_%" },
            cancellationToken: ct);
        Assert.Equal(new[] { Id("001") }, recordedIds);
    }
}
