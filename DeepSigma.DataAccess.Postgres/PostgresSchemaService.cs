using DeepSigma.DataAccess.Abstraction;
using DeepSigma.DataAccess.Abstraction.Models;
using DeepSigma.DataAccess.RelationalDatabase;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace DeepSigma.DataAccess.Postgres;

/// <summary>
/// Retrieves schema information from a PostgreSQL database: tables, fields,
/// constraints, and foreign keys. Defaults to the "public" schema.
/// </summary>
public class PostgresSchemaService : IDatabaseSchemaService
{
    private readonly RelationalDatabaseApi _api;
    private readonly string _sqlDirectory;
    private readonly ILogger<PostgresSchemaService> _logger;

    /// <summary>
    /// Initializes a new instance using a connection string.
    /// </summary>
    public PostgresSchemaService(string connectionString, ILogger<PostgresSchemaService>? logger = null)
        : this(new PostgresConnectionFactory(connectionString), logger)
    {
    }

    /// <summary>
    /// Initializes a new instance using a connection factory.
    /// </summary>
    public PostgresSchemaService(IDbConnectionFactory connectionFactory, ILogger<PostgresSchemaService>? logger = null)
    {
        _api = new RelationalDatabaseApi(connectionFactory);
        _sqlDirectory = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "SQL");
        _logger = logger ?? NullLogger<PostgresSchemaService>.Instance;
    }

    /// <inheritdoc />
    public async Task<IEnumerable<TableName>> GetTablesAsync(CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("PostgresSchemaService: executing schema query");
        string sql = await File.ReadAllTextAsync(Path.Combine(_sqlDirectory, "Postgres_TableNames.sql"), cancellationToken);
        return await _api.GetAllAsync<TableName>(sql, cancellationToken: cancellationToken);
    }

    /// <inheritdoc />
    public async Task<IEnumerable<TableField>> GetTableFieldsAsync(CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("PostgresSchemaService: executing schema query");
        string sql = await File.ReadAllTextAsync(Path.Combine(_sqlDirectory, "Postgres_TableAndFieldInfo.sql"), cancellationToken);
        return await _api.GetAllAsync<TableField>(sql, cancellationToken: cancellationToken);
    }

    /// <inheritdoc />
    public async Task<IEnumerable<TableConstraint>> GetConstraintsAsync(CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("PostgresSchemaService: executing schema query");
        string sql = await File.ReadAllTextAsync(Path.Combine(_sqlDirectory, "Postgres_Constraints.sql"), cancellationToken);
        return await _api.GetAllAsync<TableConstraint>(sql, cancellationToken: cancellationToken);
    }

    /// <inheritdoc />
    public async Task<IEnumerable<TableForeignKey>> GetForeignKeysAsync(CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("PostgresSchemaService: executing schema query");
        string sql = await File.ReadAllTextAsync(Path.Combine(_sqlDirectory, "Postgres_ForeignKeyConstraints.sql"), cancellationToken);
        return await _api.GetAllAsync<TableForeignKey>(sql, cancellationToken: cancellationToken);
    }
}
