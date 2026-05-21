using System.Data;
using DeepSigma.DataAccess.RelationalDatabase;
using Microsoft.Data.Sqlite;

namespace DeepSigma.DataAccess.Sqlite;

/// <summary>
/// Creates <see cref="SqliteConnection"/> instances for the supplied connection string.
/// Optionally invokes an <see cref="Action{T}"/> every time a connection transitions to the
/// <see cref="ConnectionState.Open"/> state — useful for per-connection PRAGMAs
/// (<c>foreign_keys = ON</c>, <c>synchronous = NORMAL</c>, <c>busy_timeout</c>, etc.).
/// </summary>
public sealed class SqliteConnectionFactory : RelationalConnectionFactoryBase<SqliteConnection>
{
    private readonly string _connectionString;

    /// <summary>Initializes a new instance of <see cref="SqliteConnectionFactory"/>.</summary>
    /// <param name="connectionString">SQLite connection string.</param>
    /// <param name="onConnectionOpened">
    /// Optional callback fired every time the connection's <c>StateChange</c> event reports a
    /// transition to <see cref="ConnectionState.Open"/>. Use to apply per-connection PRAGMAs.
    /// Persistent settings such as <c>journal_mode = WAL</c> only need to be set once per database file;
    /// transient settings such as <c>foreign_keys</c> or <c>busy_timeout</c> need to be set on every connection.
    /// </param>
    public SqliteConnectionFactory(string connectionString, Action<SqliteConnection>? onConnectionOpened = null)
        : base(onConnectionOpened)
    {
        _connectionString = connectionString;
    }

    /// <inheritdoc />
    protected override SqliteConnection CreateConnectionCore() => new(_connectionString);
}
