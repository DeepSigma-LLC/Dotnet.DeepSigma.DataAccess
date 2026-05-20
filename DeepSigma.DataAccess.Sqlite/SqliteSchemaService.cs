using DeepSigma.DataAccess.Abstraction;
using DeepSigma.DataAccess.Abstraction.Models;
using DeepSigma.DataAccess.RelationalDatabase;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace DeepSigma.DataAccess.Sqlite;

/// <summary>
/// Retrieves schema information from a SQLite database: tables, fields, constraints, and foreign keys.
/// All operations report <c>TableSchema = "main"</c> since SQLite has no real schema concept.
/// </summary>
public class SqliteSchemaService : IDatabaseSchemaService
{
    private readonly RelationalDatabaseApi _api;
    private readonly string _sqlDirectory;
    private readonly ILogger<SqliteSchemaService> _logger;

    /// <summary>
    /// Initializes a new instance using a connection string.
    /// </summary>
    public SqliteSchemaService(string connectionString, ILogger<SqliteSchemaService>? logger = null)
        : this(new SqliteConnectionFactory(connectionString), logger)
    {
    }

    /// <summary>
    /// Initializes a new instance using a connection factory.
    /// </summary>
    public SqliteSchemaService(IDbConnectionFactory connectionFactory, ILogger<SqliteSchemaService>? logger = null)
    {
        _api = new RelationalDatabaseApi(connectionFactory);
        _sqlDirectory = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "SQL");
        _logger = logger ?? NullLogger<SqliteSchemaService>.Instance;
    }

    /// <inheritdoc />
    public async Task<IEnumerable<TableName>> GetTables(CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("SqliteSchemaService: executing schema query");
        string sql = await File.ReadAllTextAsync(Path.Combine(_sqlDirectory, "Sqlite_TableNames.sql"), cancellationToken);
        return await _api.GetAllAsync<TableName>(sql, cancellationToken: cancellationToken);
    }

    /// <inheritdoc />
    public async Task<IEnumerable<TableField>> GetTableFields(CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("SqliteSchemaService: executing schema query");
        string sql = await File.ReadAllTextAsync(Path.Combine(_sqlDirectory, "Sqlite_TableAndFieldInfo.sql"), cancellationToken);
        return await _api.GetAllAsync<TableField>(sql, cancellationToken: cancellationToken);
    }

    /// <inheritdoc />
    public async Task<IEnumerable<TableConstraint>> GetConstraints(CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("SqliteSchemaService: executing schema query");
        string sql = await File.ReadAllTextAsync(Path.Combine(_sqlDirectory, "Sqlite_Constraints.sql"), cancellationToken);
        return await _api.GetAllAsync<TableConstraint>(sql, cancellationToken: cancellationToken);
    }

    /// <inheritdoc />
    public async Task<IEnumerable<TableForeignKey>> GetForeignKeys(CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("SqliteSchemaService: executing schema query");
        string sql = await File.ReadAllTextAsync(Path.Combine(_sqlDirectory, "Sqlite_ForeignKeyConstraints.sql"), cancellationToken);
        return await _api.GetAllAsync<TableForeignKey>(sql, cancellationToken: cancellationToken);
    }
}
