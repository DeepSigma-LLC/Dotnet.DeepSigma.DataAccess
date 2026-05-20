using DeepSigma.DataAccess.Abstraction;
using DeepSigma.DataAccess.Abstraction.Models;
using DeepSigma.DataAccess.RelationalDatabase;

namespace DeepSigma.DataAccess.SqlServer;

/// <summary>
/// Retrieves schema information from a SQL Server database: tables, fields,
/// constraints, and foreign keys.
/// </summary>
public class SqlServerSchemaService : IDatabaseSchemaService
{
    private readonly RelationalDatabaseAPI _api;
    private readonly string _sqlDirectory;

    /// <summary>
    /// Initializes a new instance using a connection string.
    /// </summary>
    public SqlServerSchemaService(string connectionString)
        : this(new SqlServerConnectionFactory(connectionString))
    {
    }

    /// <summary>
    /// Initializes a new instance using a connection factory.
    /// </summary>
    public SqlServerSchemaService(IDbConnectionFactory connectionFactory)
    {
        _api = new RelationalDatabaseAPI(connectionFactory);
        _sqlDirectory = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "SQL");
    }

    /// <inheritdoc />
    public async Task<IEnumerable<TableName>> GetTables()
    {
        string sql = await File.ReadAllTextAsync(Path.Combine(_sqlDirectory, "SqlServer_TableNames.sql"));
        return await _api.GetAllAsync<TableName>(sql);
    }

    /// <inheritdoc />
    public async Task<IEnumerable<TableField>> GetTableFields()
    {
        string sql = await File.ReadAllTextAsync(Path.Combine(_sqlDirectory, "SqlServer_TableAndFieldInfo.sql"));
        return await _api.GetAllAsync<TableField>(sql);
    }

    /// <inheritdoc />
    public async Task<IEnumerable<TableConstraint>> GetConstraints()
    {
        string sql = await File.ReadAllTextAsync(Path.Combine(_sqlDirectory, "SqlServer_Constraints.sql"));
        return await _api.GetAllAsync<TableConstraint>(sql);
    }

    /// <inheritdoc />
    public async Task<IEnumerable<TableForeignKey>> GetForeignKeys()
    {
        string sql = await File.ReadAllTextAsync(Path.Combine(_sqlDirectory, "SqlServer_ForeignKeyConstraints.sql"));
        return await _api.GetAllAsync<TableForeignKey>(sql);
    }
}
