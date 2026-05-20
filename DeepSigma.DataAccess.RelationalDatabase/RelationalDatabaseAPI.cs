using System.Data;
using Dapper;
using DeepSigma.DataAccess.Abstraction;

namespace DeepSigma.DataAccess.RelationalDatabase;

/// <summary>
/// Provides Dapper-backed methods to interact with a relational database:
/// querying, inserting, updating, and executing SQL commands.
/// The underlying provider is supplied by an <see cref="IDbConnectionFactory"/>.
/// </summary>
public class RelationalDatabaseAPI
{
    private readonly IDbConnectionFactory _connectionFactory;

    /// <summary>
    /// Initializes a new instance of <see cref="RelationalDatabaseAPI"/>.
    /// </summary>
    /// <param name="connectionFactory">Provider-specific connection factory.</param>
    public RelationalDatabaseAPI(IDbConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory;
    }

    /// <summary>
    /// Gets all records matching the provided SQL query and parameters.
    /// </summary>
    public async Task<IEnumerable<T>> GetAllAsync<Parameters, T>(string sql, Parameters parameters, int? command_timout = null)
    {
        using IDbConnection connection = _connectionFactory.Create();
        return await connection.QueryAsync<T>(sql, parameters, commandTimeout: command_timout);
    }

    /// <summary>
    /// Gets all records matching the provided SQL query.
    /// </summary>
    public async Task<IEnumerable<T>> GetAllAsync<T>(string sql, int? command_timout = null)
    {
        using IDbConnection connection = _connectionFactory.Create();
        return await connection.QueryAsync<T>(sql, commandTimeout: command_timout);
    }

    /// <summary>
    /// Gets a single record by its ID.
    /// </summary>
    public async Task<T?> GetByIdAsync<T>(string sql, int id, int? command_timout = null)
    {
        using IDbConnection connection = _connectionFactory.Create();
        return await connection.QueryFirstOrDefaultAsync<T>(sql, new { Id = id }, commandTimeout: command_timout);
    }

    /// <summary>
    /// Inserts a new record and returns the generated ID.
    /// </summary>
    public async Task<int> InsertAsync<Parameters>(string sql, Parameters parameters, int? command_timout = null)
    {
        using IDbConnection connection = _connectionFactory.Create();
        return await connection.ExecuteScalarAsync<int>(sql, parameters, commandTimeout: command_timout);
    }

    /// <summary>
    /// Inserts multiple records and returns the generated IDs.
    /// </summary>
    public async Task<IEnumerable<int>?> InsertAllAsync<F>(string sql, IEnumerable<F> parameters, int? command_timout = null)
    {
        using IDbConnection connection = _connectionFactory.Create();
        return await connection.ExecuteScalarAsync<IEnumerable<int>>(sql, parameters, commandTimeout: command_timout);
    }

    /// <summary>
    /// Updates an existing record and returns the number of affected rows.
    /// </summary>
    public async Task<int> UpdateAsync<Parameters>(string sql, Parameters parameters, int? command_timout = null)
    {
        using IDbConnection connection = _connectionFactory.Create();
        return await connection.ExecuteScalarAsync<int>(sql, parameters, commandTimeout: command_timout);
    }

    /// <summary>
    /// Updates multiple records and returns the number of affected rows for each update.
    /// </summary>
    public async Task<IEnumerable<int>?> UpdateAllAsync<F>(string sql, IEnumerable<F> parameters, int? command_timout = null)
    {
        using IDbConnection connection = _connectionFactory.Create();
        return await connection.ExecuteScalarAsync<IEnumerable<int>>(sql, parameters, commandTimeout: command_timout);
    }

    /// <summary>
    /// Executes a SQL command and returns a single scalar value of type T.
    /// </summary>
    public async Task<T?> ExecuteAsync<Parameters, T>(string sql, Parameters? parameters, int? command_timout = null)
    {
        using IDbConnection connection = _connectionFactory.Create();
        return await connection.ExecuteScalarAsync<T>(sql, parameters, commandTimeout: command_timout);
    }
}
