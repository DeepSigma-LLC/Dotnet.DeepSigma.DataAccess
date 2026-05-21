using System.Data;
using DeepSigma.DataAccess.RelationalDatabase;
using Microsoft.Data.SqlClient;

namespace DeepSigma.DataAccess.SqlServer;

/// <summary>
/// Creates <see cref="SqlConnection"/> instances for the supplied connection string.
/// Optionally invokes an <see cref="Action{T}"/> every time a connection transitions to the
/// <see cref="ConnectionState.Open"/> state — useful for per-connection SET statements
/// (<c>SET ANSI_NULLS ON</c>, <c>SET ARITHABORT ON</c>, etc.).
/// </summary>
public sealed class SqlServerConnectionFactory : RelationalConnectionFactoryBase<SqlConnection>
{
    private readonly string _connectionString;

    /// <summary>Initializes a new instance of <see cref="SqlServerConnectionFactory"/>.</summary>
    /// <param name="connectionString">SQL Server connection string.</param>
    /// <param name="onConnectionOpened">
    /// Optional callback fired every time the connection's <c>StateChange</c> event reports a
    /// transition to <see cref="ConnectionState.Open"/>. Use to run per-connection setup SQL.
    /// </param>
    public SqlServerConnectionFactory(string connectionString, Action<SqlConnection>? onConnectionOpened = null)
        : base(onConnectionOpened)
    {
        _connectionString = connectionString;
    }

    /// <inheritdoc />
    protected override SqlConnection CreateConnectionCore() => new(_connectionString);
}
