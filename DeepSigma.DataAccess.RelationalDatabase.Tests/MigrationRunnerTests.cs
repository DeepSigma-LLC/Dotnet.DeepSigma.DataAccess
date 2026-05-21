using System.Data;
using DeepSigma.DataAccess.Abstraction;
using Microsoft.Data.Sqlite;
using Xunit;

namespace DeepSigma.DataAccess.RelationalDatabase.Tests;

public class MigrationRunnerTests : IDisposable
{
    private const string SqliteCreateMigrationsTableSql =
        "CREATE TABLE IF NOT EXISTS _migrations (Id TEXT NOT NULL PRIMARY KEY, AppliedAtUtc TEXT NOT NULL);";

    private readonly string _connectionString;
    private readonly SqliteConnection _keepAlive;
    private readonly RelationalDatabaseApi _db;
    private readonly MigrationRunner _runner;

    public MigrationRunnerTests()
    {
        string name = $"deepsigma_migrations_tests_{Guid.NewGuid():N}";
        _connectionString = $"Data Source=file:{name}?mode=memory&cache=shared";
        _keepAlive = new SqliteConnection(_connectionString);
        _keepAlive.Open();

        IDbConnectionFactory factory = new Factory(_connectionString);
        _db = new RelationalDatabaseApi(factory);
        _runner = new MigrationRunner(_db, SqliteCreateMigrationsTableSql);
    }

    public void Dispose() => _keepAlive.Dispose();

    [Fact]
    public async Task Empty_migration_list_creates_tracking_table_and_returns_empty()
    {
        var ct = TestContext.Current.CancellationToken;

        IReadOnlyList<string> applied = await _runner.ApplyAsync(Array.Empty<Migration>(), ct);

        Assert.Empty(applied);
        long? rows = await _db.ExecuteScalarAsync<long?>(
            "SELECT COUNT(*) FROM _migrations", cancellationToken: ct);
        Assert.Equal(0, rows);
    }

    [Fact]
    public async Task Applies_all_pending_migrations_in_order()
    {
        var ct = TestContext.Current.CancellationToken;
        Migration[] migrations =
        [
            new("001", "CREATE TABLE widgets (id INTEGER PRIMARY KEY, name TEXT NOT NULL);", "create widgets"),
            new("002", "ALTER TABLE widgets ADD COLUMN price REAL NOT NULL DEFAULT 0;", "add price"),
        ];

        IReadOnlyList<string> applied = await _runner.ApplyAsync(migrations, ct);

        Assert.Equal(new[] { "001", "002" }, applied);

        await _db.ExecuteAsync(
            "INSERT INTO widgets (name, price) VALUES ('w', 1.5)",
            cancellationToken: ct);
        var row = await _db.QuerySingleOrDefaultAsync<(long id, string name, double price)>(
            "SELECT id, name, price FROM widgets",
            cancellationToken: ct);
        Assert.Equal("w", row.name);
        Assert.Equal(1.5, row.price);
    }

    [Fact]
    public async Task Already_applied_migrations_are_skipped_on_rerun()
    {
        var ct = TestContext.Current.CancellationToken;
        Migration[] first =
        [
            new("001", "CREATE TABLE a (id INTEGER PRIMARY KEY);"),
        ];
        await _runner.ApplyAsync(first, ct);

        Migration[] second =
        [
            new("001", "CREATE TABLE a (id INTEGER PRIMARY KEY);"),
            new("002", "CREATE TABLE b (id INTEGER PRIMARY KEY);"),
        ];

        IReadOnlyList<string> applied = await _runner.ApplyAsync(second, ct);

        Assert.Equal(new[] { "002" }, applied);
        long? tableCount = await _db.ExecuteScalarAsync<long?>(
            "SELECT COUNT(*) FROM sqlite_master WHERE type='table' AND name IN ('a','b')",
            cancellationToken: ct);
        Assert.Equal(2, tableCount);
    }

    [Fact]
    public async Task Failing_migration_rolls_back_and_leaves_no_record()
    {
        var ct = TestContext.Current.CancellationToken;
        Migration[] migrations =
        [
            new("001", "CREATE TABLE good (id INTEGER PRIMARY KEY);"),
            new("002", "this is not valid sql at all;"),
        ];

        await Assert.ThrowsAnyAsync<Exception>(() => _runner.ApplyAsync(migrations, ct));

        // 001 applied successfully before 002 failed.
        IEnumerable<string> recordedIds = await _db.GetAllAsync<string>(
            "SELECT Id FROM _migrations ORDER BY Id", cancellationToken: ct);
        Assert.Equal(new[] { "001" }, recordedIds);

        // 002's transaction should have rolled back — no leftover table from it.
        long? bogus = await _db.ExecuteScalarAsync<long?>(
            "SELECT COUNT(*) FROM sqlite_master WHERE type='table' AND name='bogus'",
            cancellationToken: ct);
        Assert.Equal(0, bogus);
    }

    [Fact]
    public async Task Tracking_table_creation_is_idempotent()
    {
        var ct = TestContext.Current.CancellationToken;
        await _runner.ApplyAsync(Array.Empty<Migration>(), ct);
        // Second call must not throw on the CREATE TABLE IF NOT EXISTS.
        await _runner.ApplyAsync(Array.Empty<Migration>(), ct);
    }

    private sealed class Factory : IDbConnectionFactory
    {
        private readonly string _connectionString;
        public Factory(string connectionString) => _connectionString = connectionString;
        public IDbConnection Create() => new SqliteConnection(_connectionString);
    }
}
