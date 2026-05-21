using System.Data;
using System.Data.Common;
using Dapper;
using Microsoft.Extensions.Logging;

namespace DeepSigma.DataAccess.RelationalDatabase;

/// <summary>
/// A scoped transaction over a single open <see cref="IDbConnection"/>. Mirrors the CRUD methods of
/// <see cref="RelationalDatabaseApi"/> but threads the underlying <see cref="IDbTransaction"/> through
/// every call.
/// </summary>
/// <remarks>
/// Obtain an instance via <see cref="RelationalDatabaseApi.BeginTransactionAsync"/>. Call
/// <see cref="CommitAsync"/> to commit; if the scope is disposed without committing, the transaction is rolled back.
/// Use <c>await using var tx = await db.BeginTransactionAsync(...);</c> to ensure correct disposal semantics.
/// </remarks>
public sealed class RelationalDatabaseTransactionScope : IAsyncDisposable
{
    private readonly IDbConnection _connection;
    private readonly IDbTransaction _transaction;
    private readonly ILogger _logger;
    private bool _committed;
    private bool _disposed;

    internal RelationalDatabaseTransactionScope(IDbConnection connection, IDbTransaction transaction, ILogger logger)
    {
        _connection = connection;
        _transaction = transaction;
        _logger = logger;
    }

    /// <summary>Gets all records matching the SQL + parameters, executed within the transaction.</summary>
    public async Task<IEnumerable<T>> GetAllAsync<TParam, T>(string sql, TParam parameters, int? commandTimeout = null, CommandType? commandType = null, CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("[TX] GetAllAsync<TParam, T>");
        var cmd = new CommandDefinition(sql, parameters, transaction: _transaction, commandTimeout: commandTimeout, commandType: commandType, cancellationToken: cancellationToken);
        return await _connection.QueryAsync<T>(cmd);
    }

    /// <summary>Gets all records matching the SQL, executed within the transaction.</summary>
    public async Task<IEnumerable<T>> GetAllAsync<T>(string sql, int? commandTimeout = null, CommandType? commandType = null, CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("[TX] GetAllAsync<T>");
        var cmd = new CommandDefinition(sql, transaction: _transaction, commandTimeout: commandTimeout, commandType: commandType, cancellationToken: cancellationToken);
        return await _connection.QueryAsync<T>(cmd);
    }

    /// <summary>Gets a single record by id, executed within the transaction.</summary>
    public async Task<T?> GetByIdAsync<T>(string sql, object id, int? commandTimeout = null, CommandType? commandType = null, CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("[TX] GetByIdAsync<T>");
        var cmd = new CommandDefinition(sql, new { Id = id }, transaction: _transaction, commandTimeout: commandTimeout, commandType: commandType, cancellationToken: cancellationToken);
        return await _connection.QueryFirstOrDefaultAsync<T>(cmd);
    }

    /// <summary>Returns the first row matching the SQL, or default; tolerates extras. Within the transaction.</summary>
    public async Task<T?> QueryFirstOrDefaultAsync<T>(string sql, int? commandTimeout = null, CommandType? commandType = null, CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("[TX] QueryFirstOrDefaultAsync<T>");
        var cmd = new CommandDefinition(sql, transaction: _transaction, commandTimeout: commandTimeout, commandType: commandType, cancellationToken: cancellationToken);
        return await _connection.QueryFirstOrDefaultAsync<T>(cmd);
    }

    /// <summary>Returns the first row matching the SQL + parameters, or default. Within the transaction.</summary>
    public async Task<T?> QueryFirstOrDefaultAsync<TParam, T>(string sql, TParam parameters, int? commandTimeout = null, CommandType? commandType = null, CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("[TX] QueryFirstOrDefaultAsync<TParam, T>");
        var cmd = new CommandDefinition(sql, parameters, transaction: _transaction, commandTimeout: commandTimeout, commandType: commandType, cancellationToken: cancellationToken);
        return await _connection.QueryFirstOrDefaultAsync<T>(cmd);
    }

    /// <summary>Returns the single row matching the SQL, or default; throws on >1. Within the transaction.</summary>
    public async Task<T?> QuerySingleOrDefaultAsync<T>(string sql, int? commandTimeout = null, CommandType? commandType = null, CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("[TX] QuerySingleOrDefaultAsync<T>");
        var cmd = new CommandDefinition(sql, transaction: _transaction, commandTimeout: commandTimeout, commandType: commandType, cancellationToken: cancellationToken);
        return await _connection.QuerySingleOrDefaultAsync<T>(cmd);
    }

    /// <summary>Returns the single row matching the SQL + parameters, or default; throws on >1. Within the transaction.</summary>
    public async Task<T?> QuerySingleOrDefaultAsync<TParam, T>(string sql, TParam parameters, int? commandTimeout = null, CommandType? commandType = null, CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("[TX] QuerySingleOrDefaultAsync<TParam, T>");
        var cmd = new CommandDefinition(sql, parameters, transaction: _transaction, commandTimeout: commandTimeout, commandType: commandType, cancellationToken: cancellationToken);
        return await _connection.QuerySingleOrDefaultAsync<T>(cmd);
    }

    /// <summary>Inserts a record and returns the generated id, executed within the transaction.</summary>
    public async Task<int> InsertAsync<TParam>(string sql, TParam parameters, int? commandTimeout = null, CommandType? commandType = null, CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("[TX] InsertAsync");
        var cmd = new CommandDefinition(sql, parameters, transaction: _transaction, commandTimeout: commandTimeout, commandType: commandType, cancellationToken: cancellationToken);
        return await _connection.ExecuteScalarAsync<int>(cmd);
    }

    /// <summary>Executes SQL once per parameter set within the transaction. Returns the total rows affected.</summary>
    public async Task<int> InsertAllAsync<TParam>(string sql, IEnumerable<TParam> parameters, int? commandTimeout = null, CommandType? commandType = null, CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("[TX] InsertAllAsync");
        var cmd = new CommandDefinition(sql, parameters, transaction: _transaction, commandTimeout: commandTimeout, commandType: commandType, cancellationToken: cancellationToken);
        return await _connection.ExecuteAsync(cmd);
    }

    /// <summary>Executes a non-query SQL statement within the transaction. Returns the affected row count.</summary>
    public async Task<int> UpdateAsync(string sql, int? commandTimeout = null, CommandType? commandType = null, CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("[TX] UpdateAsync");
        var cmd = new CommandDefinition(sql, transaction: _transaction, commandTimeout: commandTimeout, commandType: commandType, cancellationToken: cancellationToken);
        return await _connection.ExecuteAsync(cmd);
    }

    /// <summary>Executes an UPDATE with parameters within the transaction. Returns the affected row count.</summary>
    public async Task<int> UpdateAsync<TParam>(string sql, TParam parameters, int? commandTimeout = null, CommandType? commandType = null, CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("[TX] UpdateAsync<TParam>");
        var cmd = new CommandDefinition(sql, parameters, transaction: _transaction, commandTimeout: commandTimeout, commandType: commandType, cancellationToken: cancellationToken);
        return await _connection.ExecuteAsync(cmd);
    }

    /// <summary>Executes SQL once per parameter set within the transaction. Returns total rows affected.</summary>
    public async Task<int> UpdateAllAsync<TParam>(string sql, IEnumerable<TParam> parameters, int? commandTimeout = null, CommandType? commandType = null, CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("[TX] UpdateAllAsync");
        var cmd = new CommandDefinition(sql, parameters, transaction: _transaction, commandTimeout: commandTimeout, commandType: commandType, cancellationToken: cancellationToken);
        return await _connection.ExecuteAsync(cmd);
    }

    /// <summary>Executes a non-query SQL statement within the transaction. Returns the affected row count. Preferred over <c>UpdateAsync</c> for DELETEs, DDL, and other non-UPDATE statements.</summary>
    public async Task<int> ExecuteAsync(string sql, int? commandTimeout = null, CommandType? commandType = null, CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("[TX] ExecuteAsync");
        var cmd = new CommandDefinition(sql, transaction: _transaction, commandTimeout: commandTimeout, commandType: commandType, cancellationToken: cancellationToken);
        return await _connection.ExecuteAsync(cmd);
    }

    /// <summary>Executes a non-query SQL statement with parameters within the transaction. Returns the affected row count.</summary>
    public async Task<int> ExecuteAsync<TParam>(string sql, TParam parameters, int? commandTimeout = null, CommandType? commandType = null, CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("[TX] ExecuteAsync<TParam>");
        var cmd = new CommandDefinition(sql, parameters, transaction: _transaction, commandTimeout: commandTimeout, commandType: commandType, cancellationToken: cancellationToken);
        return await _connection.ExecuteAsync(cmd);
    }

    /// <summary>Executes SQL once per parameter set within the transaction. Returns total rows affected.</summary>
    public async Task<int> ExecuteAllAsync<TParam>(string sql, IEnumerable<TParam> parameters, int? commandTimeout = null, CommandType? commandType = null, CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("[TX] ExecuteAllAsync");
        var cmd = new CommandDefinition(sql, parameters, transaction: _transaction, commandTimeout: commandTimeout, commandType: commandType, cancellationToken: cancellationToken);
        return await _connection.ExecuteAsync(cmd);
    }

    /// <summary>Executes a SQL command and returns the first column of the first row as <typeparamref name="T"/>, within the transaction.</summary>
    public async Task<T?> ExecuteScalarAsync<T>(string sql, int? commandTimeout = null, CommandType? commandType = null, CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("[TX] ExecuteScalarAsync<T>");
        var cmd = new CommandDefinition(sql, transaction: _transaction, commandTimeout: commandTimeout, commandType: commandType, cancellationToken: cancellationToken);
        return await _connection.ExecuteScalarAsync<T>(cmd);
    }

    /// <summary>Executes a SQL command with parameters and returns the first column of the first row as <typeparamref name="T"/>, within the transaction.</summary>
    public async Task<T?> ExecuteScalarAsync<TParam, T>(string sql, TParam? parameters, int? commandTimeout = null, CommandType? commandType = null, CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("[TX] ExecuteScalarAsync<TParam, T>");
        var cmd = new CommandDefinition(sql, parameters, transaction: _transaction, commandTimeout: commandTimeout, commandType: commandType, cancellationToken: cancellationToken);
        return await _connection.ExecuteScalarAsync<T>(cmd);
    }

    /// <summary>
    /// Commits the transaction. Idempotent — subsequent calls are no-ops. Once committed, the scope's
    /// <see cref="DisposeAsync"/> will not roll back.
    /// </summary>
    public async Task CommitAsync(CancellationToken cancellationToken = default)
    {
        if (_committed) return;
        _logger.LogDebug("[TX] Commit");
        if (_transaction is DbTransaction dbTx)
        {
            await dbTx.CommitAsync(cancellationToken);
        }
        else
        {
            _transaction.Commit();
        }
        _committed = true;
    }

    /// <summary>
    /// Rolls back the transaction (if not already committed) and disposes the underlying transaction and connection.
    /// </summary>
    public async ValueTask DisposeAsync()
    {
        if (_disposed) return;
        if (!_committed)
        {
            try
            {
                _logger.LogDebug("[TX] Rolling back (scope disposed without commit)");
                if (_transaction is DbTransaction dbTx)
                {
                    await dbTx.RollbackAsync();
                }
                else
                {
                    _transaction.Rollback();
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "[TX] Rollback threw on dispose; suppressing.");
            }
        }
        _transaction.Dispose();
        _connection.Dispose();
        _disposed = true;
    }
}
