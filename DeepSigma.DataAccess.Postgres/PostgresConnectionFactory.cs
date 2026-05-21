using System.Data;
using DeepSigma.DataAccess.RelationalDatabase;
using Npgsql;

namespace DeepSigma.DataAccess.Postgres;

/// <summary>
/// Creates <see cref="NpgsqlConnection"/> instances backed by a long-lived <see cref="NpgsqlDataSource"/> —
/// the modern Npgsql idiom (Npgsql 7+). The data source owns connection pooling, type mapping configuration,
/// password rotation, and per-source logging integration.
/// </summary>
/// <remarks>
/// Two construction patterns:
/// <list type="bullet">
/// <item><description>Pass a connection string — the factory builds and owns the <see cref="NpgsqlDataSource"/>,
/// and disposes it when the factory is disposed.</description></item>
/// <item><description>Pass a <see cref="NpgsqlDataSource"/> you built yourself (e.g. via
/// <see cref="NpgsqlDataSourceBuilder"/> with custom type handlers) — the factory uses it but does <i>not</i>
/// own it. The caller is responsible for disposal.</description></item>
/// </list>
/// Optionally invokes an <see cref="Action{T}"/> every time a connection transitions to <see cref="ConnectionState.Open"/>.
/// </remarks>
public sealed class PostgresConnectionFactory : RelationalConnectionFactoryBase<NpgsqlConnection>, IDisposable
{
    private readonly NpgsqlDataSource _dataSource;
    private readonly bool _ownsDataSource;
    private bool _disposed;

    /// <summary>
    /// Initializes a new factory backed by a freshly-created <see cref="NpgsqlDataSource"/>.
    /// The factory owns the data source and disposes it when itself is disposed.
    /// </summary>
    public PostgresConnectionFactory(string connectionString, Action<NpgsqlConnection>? onConnectionOpened = null)
        : this(NpgsqlDataSource.Create(connectionString), ownsDataSource: true, onConnectionOpened)
    {
    }

    /// <summary>
    /// Initializes a new factory backed by a caller-supplied <see cref="NpgsqlDataSource"/>.
    /// The caller retains ownership of the data source and is responsible for disposing it.
    /// </summary>
    public PostgresConnectionFactory(NpgsqlDataSource dataSource, Action<NpgsqlConnection>? onConnectionOpened = null)
        : this(dataSource, ownsDataSource: false, onConnectionOpened)
    {
    }

    /// <summary>
    /// Initializes a new factory backed by the supplied <see cref="NpgsqlDataSource"/> with explicit
    /// ownership control. Use when the data source was built inside a factory method (e.g. a DI lambda)
    /// that has no other owner — pass <c>ownsDataSource: true</c> so disposal flows through this factory.
    /// </summary>
    public PostgresConnectionFactory(
        NpgsqlDataSource dataSource,
        bool ownsDataSource,
        Action<NpgsqlConnection>? onConnectionOpened = null)
        : base(onConnectionOpened)
    {
        _dataSource = dataSource;
        _ownsDataSource = ownsDataSource;
    }

    /// <inheritdoc />
    protected override NpgsqlConnection CreateConnectionCore() => _dataSource.CreateConnection();

    /// <summary>
    /// Disposes the underlying <see cref="NpgsqlDataSource"/> if this factory owns it.
    /// No-op if the data source was supplied externally.
    /// </summary>
    public void Dispose()
    {
        if (_disposed) return;
        if (_ownsDataSource)
        {
            _dataSource.Dispose();
        }
        _disposed = true;
    }
}
