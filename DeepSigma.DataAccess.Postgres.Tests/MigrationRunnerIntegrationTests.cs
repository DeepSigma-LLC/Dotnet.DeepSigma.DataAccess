using DeepSigma.DataAccess.Abstraction;
using DeepSigma.DataAccess.RelationalDatabase;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace DeepSigma.DataAccess.Postgres.Tests;

/// <summary>
/// Verifies that the Postgres-flavoured <c>_migrations</c> DDL registered by
/// <c>AddDeepSigmaPostgres</c> actually parses and that <see cref="MigrationRunner"/> applies
/// real DDL through Npgsql end-to-end.
/// </summary>
/// <remarks>
/// Requires a running PostgreSQL instance reachable at the connection string below. Override the
/// <c>DEEPSIGMA_POSTGRES_CONNECTION</c> environment variable to point at your own server.
/// Each test creates uniquely-named tables (Guid-suffixed) and cleans them up — and its tracking
/// rows — on dispose, so reruns and parallel runs against the same database are safe.
/// </remarks>
[Trait("Category", "Integration")]
public class MigrationRunnerIntegrationTests : IAsyncDisposable
{
    private static readonly string ConnectionString =
        Environment.GetEnvironmentVariable("DEEPSIGMA_POSTGRES_CONNECTION")
        ?? "Host=localhost;Port=5432;Database=postgres;Username=postgres;Password=postgres";

    private readonly string _suffix = Guid.NewGuid().ToString("N");
    private readonly ServiceProvider _provider;
    private readonly MigrationRunner _runner;
    private readonly RelationalDatabaseApi _db;

    private string T(string name) => $"deepsigma_mr_{name}_{_suffix}";
    private string Id(string name) => $"{_suffix}_{name}";

    public MigrationRunnerIntegrationTests()
    {
        var services = new ServiceCollection();
        services.AddDeepSigmaPostgres(ConnectionString);
        _provider = services.BuildServiceProvider();
        _runner = _provider.GetRequiredService<MigrationRunner>();
        _db = _provider.GetRequiredService<RelationalDatabaseApi>();
    }

    public async ValueTask DisposeAsync()
    {
        // Best-effort cleanup: drop test tables, remove our tracking rows.
        try
        {
            await _db.ExecuteAsync($"DROP TABLE IF EXISTS {T("widgets")}");
            await _db.ExecuteAsync($"DROP TABLE IF EXISTS {T("gadgets")}");
            await _db.ExecuteAsync(
                "DELETE FROM _migrations WHERE Id LIKE @Prefix",
                new { Prefix = $"{_suffix}_%" });
        }
        catch
        {
            // Ignore — cleanup is best-effort against potentially-broken test state.
        }
        await _provider.DisposeAsync();
    }

    [Fact]
    public async Task Creates_tracking_table_and_applies_a_migration()
    {
        var ct = TestContext.Current.CancellationToken;
        Migration[] migrations =
        [
            new(Id("001"), $"CREATE TABLE {T("widgets")} (id SERIAL PRIMARY KEY, name TEXT NOT NULL);", "create widgets"),
        ];

        IReadOnlyList<string> applied = await _runner.ApplyAsync(migrations, ct);

        Assert.Equal(new[] { Id("001") }, applied);

        await _db.ExecuteAsync(
            $"INSERT INTO {T("widgets")} (name) VALUES (@Name)",
            new { Name = "w" }, cancellationToken: ct);
        long? count = await _db.ExecuteScalarAsync<long?>(
            $"SELECT COUNT(*) FROM {T("widgets")}", cancellationToken: ct);
        Assert.Equal(1, count);
    }

    [Fact]
    public async Task Tracking_table_is_idempotent_across_runs()
    {
        var ct = TestContext.Current.CancellationToken;
        Migration[] first =
        [
            new(Id("001"), $"CREATE TABLE {T("widgets")} (id SERIAL PRIMARY KEY);"),
        ];
        await _runner.ApplyAsync(first, ct);

        Migration[] second =
        [
            new(Id("001"), $"CREATE TABLE {T("widgets")} (id SERIAL PRIMARY KEY);"),
            new(Id("002"), $"CREATE TABLE {T("gadgets")} (id SERIAL PRIMARY KEY);"),
        ];
        IReadOnlyList<string> applied = await _runner.ApplyAsync(second, ct);

        Assert.Equal(new[] { Id("002") }, applied);

        long? widgets = await _db.ExecuteScalarAsync<long?>(
            $"SELECT COUNT(*) FROM information_schema.tables WHERE table_name = '{T("widgets")}'",
            cancellationToken: ct);
        long? gadgets = await _db.ExecuteScalarAsync<long?>(
            $"SELECT COUNT(*) FROM information_schema.tables WHERE table_name = '{T("gadgets")}'",
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
            new(Id("001"), $"CREATE TABLE {T("widgets")} (id SERIAL PRIMARY KEY);"),
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
