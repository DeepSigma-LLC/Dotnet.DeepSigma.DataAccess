using System.Data;
using DeepSigma.DataAccess.Abstraction;
using Microsoft.Data.Sqlite;

namespace DeepSigma.DataAccess.RelationalDatabase.Tests.Infrastructure;

/// <summary>
/// Per-class fixture that creates an isolated SQLite shared-memory database with a known schema
/// and exposes an <see cref="IDbConnectionFactory"/> ready for use with <see cref="RelationalDatabaseApi"/>.
/// Holds a keep-alive connection so the shared in-memory DB persists across test methods.
/// </summary>
public sealed class SqliteTestHarness : IDisposable
{
    public string ConnectionString { get; }
    public IDbConnectionFactory Factory { get; }
    private readonly SqliteConnection _keepAlive;

    public SqliteTestHarness()
    {
        string name = $"deepsigma_reldb_tests_{Guid.NewGuid():N}";
        ConnectionString = $"Data Source=file:{name}?mode=memory&cache=shared";
        _keepAlive = new SqliteConnection(ConnectionString);
        _keepAlive.Open();
        SeedSchema(_keepAlive);
        Factory = new SqliteFactory(ConnectionString);
    }

    private static void SeedSchema(SqliteConnection conn)
    {
        using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            CREATE TABLE items (
                id INTEGER PRIMARY KEY AUTOINCREMENT,
                name TEXT NOT NULL,
                price REAL NOT NULL DEFAULT 0
            );

            INSERT INTO items (name, price) VALUES ('apple', 1.0);
            INSERT INTO items (name, price) VALUES ('banana', 0.5);
            INSERT INTO items (name, price) VALUES ('cherry', 2.0);
            """;
        cmd.ExecuteNonQuery();
    }

    public void Dispose() => _keepAlive.Dispose();

    private sealed class SqliteFactory : IDbConnectionFactory
    {
        private readonly string _connectionString;
        public SqliteFactory(string connectionString) => _connectionString = connectionString;
        public IDbConnection Create() => new SqliteConnection(_connectionString);
    }
}
