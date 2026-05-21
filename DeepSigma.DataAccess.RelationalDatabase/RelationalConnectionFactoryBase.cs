using System.Data;
using System.Data.Common;
using DeepSigma.DataAccess.Abstraction;

namespace DeepSigma.DataAccess.RelationalDatabase;

/// <summary>
/// Base class for relational <see cref="IDbConnectionFactory"/> implementations. Handles the
/// boilerplate of wiring an optional "on connection opened" callback to ADO.NET's
/// <see cref="DbConnection.StateChange"/> event so per-connection setup SQL (SET / PRAGMA / etc.)
/// runs every time the pool hands out a freshly-opened connection.
/// </summary>
/// <typeparam name="TConnection">
/// The provider-specific connection type (e.g. <c>SqlConnection</c>, <c>NpgsqlConnection</c>,
/// <c>SqliteConnection</c>). Must derive from <see cref="DbConnection"/> so the StateChange event
/// is reachable.
/// </typeparam>
public abstract class RelationalConnectionFactoryBase<TConnection> : IDbConnectionFactory
    where TConnection : DbConnection
{
    private readonly Action<TConnection>? _onConnectionOpened;

    /// <summary>Initializes the base with an optional on-open callback.</summary>
    protected RelationalConnectionFactoryBase(Action<TConnection>? onConnectionOpened)
    {
        _onConnectionOpened = onConnectionOpened;
    }

    /// <inheritdoc />
    public IDbConnection Create()
    {
        TConnection connection = CreateConnectionCore();
        if (_onConnectionOpened is not null)
        {
            connection.StateChange += OnStateChange;
        }
        return connection;
    }

    /// <summary>
    /// Produces a fresh, unopened (or pool-recycled) provider-specific connection. Implementors
    /// should not touch <see cref="DbConnection.StateChange"/> — the base class wires it.
    /// </summary>
    protected abstract TConnection CreateConnectionCore();

    private void OnStateChange(object? sender, StateChangeEventArgs args)
    {
        if (args.CurrentState == ConnectionState.Open && sender is TConnection conn)
        {
            _onConnectionOpened!(conn);
        }
    }
}
