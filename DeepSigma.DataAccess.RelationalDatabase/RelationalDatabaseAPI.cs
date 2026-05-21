using System.Data;
using System.Data.Common;
using System.Runtime.CompilerServices;
using Dapper;
using DeepSigma.DataAccess.Abstraction;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace DeepSigma.DataAccess.RelationalDatabase;

/// <summary>
/// Provides Dapper-backed methods to interact with a relational database:
/// querying, inserting, updating, executing SQL commands, streaming results, and running transactions.
/// The underlying provider is supplied by an <see cref="IDbConnectionFactory"/>.
/// </summary>
public sealed class RelationalDatabaseApi
{
    private readonly IDbConnectionFactory _connectionFactory;
    private readonly ILogger<RelationalDatabaseApi> _logger;

    /// <summary>
    /// Initializes a new instance of <see cref="RelationalDatabaseApi"/>.
    /// </summary>
    /// <param name="connectionFactory">Provider-specific connection factory.</param>
    /// <param name="logger">Optional logger. Defaults to <see cref="NullLogger{T}.Instance"/>.</param>
    public RelationalDatabaseApi(IDbConnectionFactory connectionFactory, ILogger<RelationalDatabaseApi>? logger = null)
    {
        _connectionFactory = connectionFactory;
        _logger = logger ?? NullLogger<RelationalDatabaseApi>.Instance;
    }

    /// <summary>
    /// Gets all records matching the provided SQL query and parameters.
    /// </summary>
    public async Task<IEnumerable<T>> GetAllAsync<TParam, T>(string sql, TParam parameters, int? commandTimeout = null, CommandType? commandType = null, CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("GetAllAsync<TParam, T> executing");
        using IDbConnection connection = _connectionFactory.Create();
        var cmd = new CommandDefinition(sql, parameters, commandTimeout: commandTimeout, commandType: commandType, cancellationToken: cancellationToken);
        return await connection.QueryAsync<T>(cmd);
    }

    /// <summary>
    /// Gets all records matching the provided SQL query.
    /// </summary>
    public async Task<IEnumerable<T>> GetAllAsync<T>(string sql, int? commandTimeout = null, CommandType? commandType = null, CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("GetAllAsync<T> executing");
        using IDbConnection connection = _connectionFactory.Create();
        var cmd = new CommandDefinition(sql, commandTimeout: commandTimeout, commandType: commandType, cancellationToken: cancellationToken);
        return await connection.QueryAsync<T>(cmd);
    }

    /// <summary>
    /// Gets a single record by its ID. The SQL is expected to reference an <c>@Id</c> parameter.
    /// The id is bound as <c>object</c>, so any type Dapper can serialize (int, long, Guid, string, ...) is supported.
    /// This is a convenience wrapper around <see cref="QueryFirstOrDefaultAsync{TParam, T}"/>.
    /// </summary>
    public async Task<T?> GetByIdAsync<T>(string sql, object id, int? commandTimeout = null, CommandType? commandType = null, CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("GetByIdAsync<T> executing");
        using IDbConnection connection = _connectionFactory.Create();
        var cmd = new CommandDefinition(sql, new { Id = id }, commandTimeout: commandTimeout, commandType: commandType, cancellationToken: cancellationToken);
        return await connection.QueryFirstOrDefaultAsync<T>(cmd);
    }

    /// <summary>
    /// Returns the first record matching the SQL, or <c>default</c> when none exist. Tolerates multiple rows
    /// (only the first is returned). Use when "give me one matching row" is the intent and extras are OK.
    /// </summary>
    public async Task<T?> QueryFirstOrDefaultAsync<T>(string sql, int? commandTimeout = null, CommandType? commandType = null, CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("QueryFirstOrDefaultAsync<T> executing");
        using IDbConnection connection = _connectionFactory.Create();
        var cmd = new CommandDefinition(sql, commandTimeout: commandTimeout, commandType: commandType, cancellationToken: cancellationToken);
        return await connection.QueryFirstOrDefaultAsync<T>(cmd);
    }

    /// <summary>
    /// Returns the first record matching the SQL + parameters, or <c>default</c> when none exist.
    /// Tolerates multiple rows (only the first is returned).
    /// </summary>
    public async Task<T?> QueryFirstOrDefaultAsync<TParam, T>(string sql, TParam parameters, int? commandTimeout = null, CommandType? commandType = null, CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("QueryFirstOrDefaultAsync<TParam, T> executing");
        using IDbConnection connection = _connectionFactory.Create();
        var cmd = new CommandDefinition(sql, parameters, commandTimeout: commandTimeout, commandType: commandType, cancellationToken: cancellationToken);
        return await connection.QueryFirstOrDefaultAsync<T>(cmd);
    }

    /// <summary>
    /// Returns the single record matching the SQL, or <c>default</c> when none exist.
    /// Throws <see cref="InvalidOperationException"/> if more than one row is returned —
    /// use this when you expect "exactly zero or one" (e.g. uniqueness checks, lookups by unique key).
    /// </summary>
    public async Task<T?> QuerySingleOrDefaultAsync<T>(string sql, int? commandTimeout = null, CommandType? commandType = null, CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("QuerySingleOrDefaultAsync<T> executing");
        using IDbConnection connection = _connectionFactory.Create();
        var cmd = new CommandDefinition(sql, commandTimeout: commandTimeout, commandType: commandType, cancellationToken: cancellationToken);
        return await connection.QuerySingleOrDefaultAsync<T>(cmd);
    }

    /// <summary>
    /// Returns the single record matching the SQL + parameters, or <c>default</c> when none exist.
    /// Throws <see cref="InvalidOperationException"/> if more than one row is returned.
    /// </summary>
    public async Task<T?> QuerySingleOrDefaultAsync<TParam, T>(string sql, TParam parameters, int? commandTimeout = null, CommandType? commandType = null, CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("QuerySingleOrDefaultAsync<TParam, T> executing");
        using IDbConnection connection = _connectionFactory.Create();
        var cmd = new CommandDefinition(sql, parameters, commandTimeout: commandTimeout, commandType: commandType, cancellationToken: cancellationToken);
        return await connection.QuerySingleOrDefaultAsync<T>(cmd);
    }

    /// <summary>
    /// Inserts a new record and returns the generated ID.
    /// </summary>
    public async Task<int> InsertAsync<TParam>(string sql, TParam parameters, int? commandTimeout = null, CommandType? commandType = null, CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("InsertAsync executing");
        using IDbConnection connection = _connectionFactory.Create();
        var cmd = new CommandDefinition(sql, parameters, commandTimeout: commandTimeout, commandType: commandType, cancellationToken: cancellationToken);
        return await connection.ExecuteScalarAsync<int>(cmd);
    }

    /// <summary>
    /// Executes the SQL once per parameter set and returns the total number of rows affected.
    /// Use this for bulk inserts where you do not need the generated identifiers back.
    /// </summary>
    public async Task<int> InsertAllAsync<TParam>(string sql, IEnumerable<TParam> parameters, int? commandTimeout = null, CommandType? commandType = null, CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("InsertAllAsync executing");
        using IDbConnection connection = _connectionFactory.Create();
        var cmd = new CommandDefinition(sql, parameters, commandTimeout: commandTimeout, commandType: commandType, cancellationToken: cancellationToken);
        return await connection.ExecuteAsync(cmd);
    }

    /// <summary>
    /// Executes a non-query SQL statement and returns the number of affected rows.
    /// Suitable for parameterless UPDATEs, DELETEs, or DDL such as <c>CREATE TABLE</c> / <c>ALTER TABLE</c>.
    /// (Note: many providers report <c>-1</c> rows-affected for DDL — the value is meaningful for DML only.)
    /// </summary>
    /// <remarks>
    /// <see cref="ExecuteAsync(string, int?, System.Data.CommandType?, CancellationToken)"/> is the preferred
    /// name for new code (matches Dapper convention). <c>UpdateAsync</c> remains for back-compatibility and
    /// reads naturally for actual UPDATE statements.
    /// </remarks>
    public async Task<int> UpdateAsync(string sql, int? commandTimeout = null, CommandType? commandType = null, CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("UpdateAsync executing");
        using IDbConnection connection = _connectionFactory.Create();
        var cmd = new CommandDefinition(sql, commandTimeout: commandTimeout, commandType: commandType, cancellationToken: cancellationToken);
        return await connection.ExecuteAsync(cmd);
    }

    /// <summary>
    /// Executes an UPDATE statement with parameters and returns the number of affected rows.
    /// </summary>
    /// <remarks>
    /// <see cref="ExecuteAsync{TParam}(string, TParam, int?, System.Data.CommandType?, CancellationToken)"/>
    /// is the preferred name for new code; <c>UpdateAsync</c> remains for back-compatibility.
    /// </remarks>
    public async Task<int> UpdateAsync<TParam>(string sql, TParam parameters, int? commandTimeout = null, CommandType? commandType = null, CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("UpdateAsync<TParam> executing");
        using IDbConnection connection = _connectionFactory.Create();
        var cmd = new CommandDefinition(sql, parameters, commandTimeout: commandTimeout, commandType: commandType, cancellationToken: cancellationToken);
        return await connection.ExecuteAsync(cmd);
    }

    /// <summary>
    /// Executes the SQL once per parameter set and returns the total number of rows affected across all sets.
    /// </summary>
    /// <remarks>
    /// <see cref="ExecuteAllAsync{TParam}(string, IEnumerable{TParam}, int?, System.Data.CommandType?, CancellationToken)"/>
    /// is the preferred name for new code; <c>UpdateAllAsync</c> remains for back-compatibility.
    /// </remarks>
    public async Task<int> UpdateAllAsync<TParam>(string sql, IEnumerable<TParam> parameters, int? commandTimeout = null, CommandType? commandType = null, CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("UpdateAllAsync executing");
        using IDbConnection connection = _connectionFactory.Create();
        var cmd = new CommandDefinition(sql, parameters, commandTimeout: commandTimeout, commandType: commandType, cancellationToken: cancellationToken);
        return await connection.ExecuteAsync(cmd);
    }

    /// <summary>
    /// Executes a non-query SQL statement and returns the number of affected rows.
    /// Preferred general-purpose non-query executor — matches Dapper's <c>ExecuteAsync</c> convention.
    /// Use for <c>DELETE</c>, <c>INSERT … ON CONFLICT</c>, DDL, or any other statement where you don't need a return value beyond row count.
    /// </summary>
    public async Task<int> ExecuteAsync(string sql, int? commandTimeout = null, CommandType? commandType = null, CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("ExecuteAsync executing");
        using IDbConnection connection = _connectionFactory.Create();
        var cmd = new CommandDefinition(sql, commandTimeout: commandTimeout, commandType: commandType, cancellationToken: cancellationToken);
        return await connection.ExecuteAsync(cmd);
    }

    /// <summary>
    /// Executes a non-query SQL statement with parameters and returns the number of affected rows.
    /// </summary>
    public async Task<int> ExecuteAsync<TParam>(string sql, TParam parameters, int? commandTimeout = null, CommandType? commandType = null, CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("ExecuteAsync<TParam> executing");
        using IDbConnection connection = _connectionFactory.Create();
        var cmd = new CommandDefinition(sql, parameters, commandTimeout: commandTimeout, commandType: commandType, cancellationToken: cancellationToken);
        return await connection.ExecuteAsync(cmd);
    }

    /// <summary>
    /// Executes the SQL once per parameter set and returns the total number of rows affected across all sets.
    /// </summary>
    public async Task<int> ExecuteAllAsync<TParam>(string sql, IEnumerable<TParam> parameters, int? commandTimeout = null, CommandType? commandType = null, CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("ExecuteAllAsync executing");
        using IDbConnection connection = _connectionFactory.Create();
        var cmd = new CommandDefinition(sql, parameters, commandTimeout: commandTimeout, commandType: commandType, cancellationToken: cancellationToken);
        return await connection.ExecuteAsync(cmd);
    }

    /// <summary>
    /// Executes a SQL command and returns the first column of the first row as <typeparamref name="T"/>.
    /// Typical use: <c>SELECT COUNT(*)</c>, <c>SELECT MAX(...)</c>, single-value lookups.
    /// </summary>
    public async Task<T?> ExecuteScalarAsync<T>(string sql, int? commandTimeout = null, CommandType? commandType = null, CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("ExecuteScalarAsync<T> executing");
        using IDbConnection connection = _connectionFactory.Create();
        var cmd = new CommandDefinition(sql, commandTimeout: commandTimeout, commandType: commandType, cancellationToken: cancellationToken);
        return await connection.ExecuteScalarAsync<T>(cmd);
    }

    /// <summary>
    /// Executes a SQL command with parameters and returns the first column of the first row as <typeparamref name="T"/>.
    /// </summary>
    public async Task<T?> ExecuteScalarAsync<TParam, T>(string sql, TParam? parameters, int? commandTimeout = null, CommandType? commandType = null, CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("ExecuteScalarAsync<TParam, T> executing");
        using IDbConnection connection = _connectionFactory.Create();
        var cmd = new CommandDefinition(sql, parameters, commandTimeout: commandTimeout, commandType: commandType, cancellationToken: cancellationToken);
        return await connection.ExecuteScalarAsync<T>(cmd);
    }

    /// <summary>
    /// Streams query results as <see cref="IAsyncEnumerable{T}"/> so the caller can process rows without
    /// materializing the entire result set in memory. The connection stays open until enumeration completes
    /// or the enumerator is disposed.
    /// </summary>
    public async IAsyncEnumerable<T> QueryStreamAsync<T>(string sql, int? commandTimeout = null, CommandType? commandType = null, [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("QueryStreamAsync<T> executing");
        using IDbConnection connection = _connectionFactory.Create();
        if (connection is not DbConnection dbConnection)
        {
            throw new InvalidOperationException(
                $"QueryStreamAsync requires the connection factory to return a {nameof(DbConnection)}-derived type; got {connection.GetType().Name}.");
        }
        await foreach (T item in dbConnection.QueryUnbufferedAsync<T>(sql, commandTimeout: commandTimeout, commandType: commandType).WithCancellation(cancellationToken))
        {
            yield return item;
        }
    }

    /// <summary>
    /// Streams query results with parameters as <see cref="IAsyncEnumerable{T}"/>.
    /// </summary>
    public async IAsyncEnumerable<T> QueryStreamAsync<TParam, T>(string sql, TParam parameters, int? commandTimeout = null, CommandType? commandType = null, [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("QueryStreamAsync<TParam, T> executing");
        using IDbConnection connection = _connectionFactory.Create();
        if (connection is not DbConnection dbConnection)
        {
            throw new InvalidOperationException(
                $"QueryStreamAsync requires the connection factory to return a {nameof(DbConnection)}-derived type; got {connection.GetType().Name}.");
        }
        await foreach (T item in dbConnection.QueryUnbufferedAsync<T>(sql, parameters, commandTimeout: commandTimeout, commandType: commandType).WithCancellation(cancellationToken))
        {
            yield return item;
        }
    }

    /// <summary>
    /// Begins a database transaction over a freshly-opened connection. Returns a
    /// <see cref="RelationalDatabaseTransactionScope"/> that mirrors the CRUD methods and threads the
    /// transaction through each call.
    /// </summary>
    /// <param name="isolationLevel">Optional isolation level. Defaults to the provider's default.</param>
    /// <param name="cancellationToken">Cancellation token honoured during connection open.</param>
    /// <example>
    /// <code>
    /// await using var tx = await db.BeginTransactionAsync(cancellationToken: ct);
    /// await tx.InsertAsync(insertSql, p1, cancellationToken: ct);
    /// await tx.UpdateAsync(updateSql, p2, cancellationToken: ct);
    /// await tx.CommitAsync(ct);
    /// </code>
    /// </example>
    public async Task<RelationalDatabaseTransactionScope> BeginTransactionAsync(IsolationLevel? isolationLevel = null, CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("BeginTransactionAsync (isolation={Isolation})", isolationLevel?.ToString() ?? "<default>");
        IDbConnection connection = _connectionFactory.Create();
        if (connection is DbConnection dbConnection)
        {
            await dbConnection.OpenAsync(cancellationToken);
        }
        else
        {
            connection.Open();
        }
        IDbTransaction transaction = isolationLevel.HasValue
            ? connection.BeginTransaction(isolationLevel.Value)
            : connection.BeginTransaction();
        return new RelationalDatabaseTransactionScope(connection, transaction, _logger);
    }
}
