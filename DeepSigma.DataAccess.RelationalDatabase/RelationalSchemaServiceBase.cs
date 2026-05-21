using DeepSigma.DataAccess.Abstraction;
using DeepSigma.DataAccess.Abstraction.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace DeepSigma.DataAccess.RelationalDatabase;

/// <summary>
/// Base class for relational <see cref="IDatabaseSchemaService"/> implementations that introspect
/// the database via packaged <c>.sql</c> files. The four <c>GetXxxAsync</c> methods are wired here;
/// concrete subclasses only supply a file-name prefix (e.g. <c>"Postgres"</c>, <c>"SqlServer"</c>,
/// <c>"Sqlite"</c>) that selects which packaged SQL file to load.
/// </summary>
/// <remarks>
/// The SQL files are expected at <c>{AppDomain.BaseDirectory}/SQL/{prefix}_TableNames.sql</c>,
/// <c>{prefix}_TableAndFieldInfo.sql</c>, <c>{prefix}_Constraints.sql</c>, and
/// <c>{prefix}_ForeignKeyConstraints.sql</c>. Provider packages ship these via
/// <c>&lt;None CopyToOutputDirectory&gt;</c> entries in the csproj.
/// </remarks>
public abstract class RelationalSchemaServiceBase : IDatabaseSchemaService
{
    private readonly RelationalDatabaseApi _api;
    private readonly string _sqlDirectory;
    private readonly string _filePrefix;
    private readonly ILogger _logger;

    /// <summary>Initializes a new instance.</summary>
    /// <param name="connectionFactory">Provider-specific connection factory.</param>
    /// <param name="filePrefix">
    /// Provider name used as the SQL file prefix (e.g. <c>"Postgres"</c>). The resolved file paths are
    /// <c>{AppDomain.BaseDirectory}/SQL/{filePrefix}_*.sql</c>.
    /// </param>
    /// <param name="logger">Optional logger; defaults to <see cref="NullLogger.Instance"/>.</param>
    protected RelationalSchemaServiceBase(
        IDbConnectionFactory connectionFactory,
        string filePrefix,
        ILogger? logger = null)
    {
        _api = new RelationalDatabaseApi(connectionFactory);
        _sqlDirectory = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "SQL");
        _filePrefix = filePrefix;
        _logger = logger ?? NullLogger.Instance;
    }

    /// <inheritdoc />
    public Task<IEnumerable<TableName>> GetTablesAsync(CancellationToken cancellationToken = default)
        => QuerySchemaFileAsync<TableName>("TableNames", cancellationToken);

    /// <inheritdoc />
    public Task<IEnumerable<TableField>> GetTableFieldsAsync(CancellationToken cancellationToken = default)
        => QuerySchemaFileAsync<TableField>("TableAndFieldInfo", cancellationToken);

    /// <inheritdoc />
    public Task<IEnumerable<TableConstraint>> GetConstraintsAsync(CancellationToken cancellationToken = default)
        => QuerySchemaFileAsync<TableConstraint>("Constraints", cancellationToken);

    /// <inheritdoc />
    public Task<IEnumerable<TableForeignKey>> GetForeignKeysAsync(CancellationToken cancellationToken = default)
        => QuerySchemaFileAsync<TableForeignKey>("ForeignKeyConstraints", cancellationToken);

    private async Task<IEnumerable<T>> QuerySchemaFileAsync<T>(string fileSuffix, CancellationToken cancellationToken)
    {
        string path = Path.Combine(_sqlDirectory, $"{_filePrefix}_{fileSuffix}.sql");
        _logger.LogDebug("{Prefix}SchemaService: executing schema query from {Path}", _filePrefix, path);
        string sql = await File.ReadAllTextAsync(path, cancellationToken);
        return await _api.GetAllAsync<T>(sql, cancellationToken: cancellationToken);
    }
}
