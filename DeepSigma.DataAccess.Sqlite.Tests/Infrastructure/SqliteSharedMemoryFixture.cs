using Microsoft.Data.Sqlite;

namespace DeepSigma.DataAccess.Sqlite.Tests.Infrastructure;

/// <summary>
/// Per-class fixture that creates an isolated SQLite shared-memory database with a known schema.
/// Holds a keep-alive connection so the shared in-memory DB is not reaped between test methods.
/// </summary>
public sealed class SqliteSharedMemoryFixture : IDisposable
{
    public string ConnectionString { get; }
    private readonly SqliteConnection _keepAlive;

    public SqliteSharedMemoryFixture()
    {
        string name = $"deepsigma_sqlite_tests_{Guid.NewGuid():N}";
        ConnectionString = $"Data Source=file:{name}?mode=memory&cache=shared";
        _keepAlive = new SqliteConnection(ConnectionString);
        _keepAlive.Open();
        SeedSchema(_keepAlive);
    }

    private static void SeedSchema(SqliteConnection conn)
    {
        using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            CREATE TABLE users (
                id INTEGER PRIMARY KEY,
                name TEXT NOT NULL,
                email TEXT UNIQUE,
                created_at TEXT NOT NULL DEFAULT (datetime('now'))
            );

            CREATE TABLE orders (
                id INTEGER PRIMARY KEY,
                user_id INTEGER NOT NULL REFERENCES users(id),
                amount REAL NOT NULL,
                placed_at TEXT NOT NULL DEFAULT (datetime('now'))
            );

            INSERT INTO users (id, name, email) VALUES (1, 'Ada', 'ada@example.com');
            INSERT INTO users (id, name, email) VALUES (2, 'Linus', 'linus@example.com');
            INSERT INTO orders (id, user_id, amount) VALUES (1, 1, 99.50);
            """;
        cmd.ExecuteNonQuery();
    }

    public void Dispose() => _keepAlive.Dispose();
}
