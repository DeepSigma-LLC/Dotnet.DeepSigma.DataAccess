using DeepSigma.DataAccess.Abstraction;
using DeepSigma.DataAccess.Abstraction.Models;
using DeepSigma.DataAccess.RelationalDatabase;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace DeepSigma.DataAccess.SqlServer;

/// <summary>
/// Retrieves schema information from a SQL Server database: tables, fields,
/// constraints, and foreign keys.
/// </summary>
public class SqlServerSchemaService : IDatabaseSchemaService
{
    private readonly RelationalDatabaseApi _api;
    private readonly string _sqlDirectory;
    private readonly ILogger<SqlServerSchemaService> _logger;

    /// <summary>
    /// Initializes a new instance using a connection string.
    /// </summary>
    public SqlServerSchemaService(string connectionString, ILogger<SqlServerSchemaService>? logger = null)
        : this(new SqlServerConnectionFactory(connectionString), logger)
    {
    }

    /// <summary>
    /// Initializes a new instance using a connection factory.
    /// </summary>
    public SqlServerSchemaService(IDbConnectionFactory connectionFactory, ILogger<SqlServerSchemaService>? logger = null)
    {
        _api = new RelationalDatabaseApi(connectionFactory);
        _sqlDirectory = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "SQL");
        _logger = logger ?? NullLogger<SqlServerSchemaService>.Instance;
    }

    /// <inheritdoc />
    public async Task<IEnumerable<TableName>> GetTablesAsync(CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("SqlServerSchemaService: executing schema query");
        string sql = await File.ReadAllTextAsync(Path.Combine(_sqlDirectory, "SqlServer_TableNames.sql"), cancellationToken);
        return await _api.GetAllAsync<TableName>(sql, cancellationToken: cancellationToken);
    }

    /// <inheritdoc />
    public async Task<IEnumerable<TableField>> GetTableFieldsAsync(CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("SqlServerSchemaService: executing schema query");
        string sql = await File.ReadAllTextAsync(Path.Combine(_sqlDirectory, "SqlServer_TableAndFieldInfo.sql"), cancellationToken);
        return await _api.GetAllAsync<TableField>(sql, cancellationToken: cancellationToken);
    }

    /// <inheritdoc />
    public async Task<IEnumerable<TableConstraint>> GetConstraintsAsync(CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("SqlServerSchemaService: executing schema query");
        string sql = await File.ReadAllTextAsync(Path.Combine(_sqlDirectory, "SqlServer_Constraints.sql"), cancellationToken);
        return await _api.GetAllAsync<TableConstraint>(sql, cancellationToken: cancellationToken);
    }

    /// <inheritdoc />
    public async Task<IEnumerable<TableForeignKey>> GetForeignKeysAsync(CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("SqlServerSchemaService: executing schema query");
        string sql = await File.ReadAllTextAsync(Path.Combine(_sqlDirectory, "SqlServer_ForeignKeyConstraints.sql"), cancellationToken);
        return await _api.GetAllAsync<TableForeignKey>(sql, cancellationToken: cancellationToken);
    }
}
