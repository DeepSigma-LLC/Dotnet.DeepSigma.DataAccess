using DeepSigma.DataAccess.Abstraction;
using DeepSigma.DataAccess.Abstraction.Models;
using DeepSigma.DataAccess.RelationalDatabase;

namespace DeepSigma.DataAccess.Postgres;

/// <summary>
/// Retrieves schema information from a PostgreSQL database: tables, fields,
/// constraints, and foreign keys. Defaults to the "public" schema.
/// </summary>
public class PostgresSchemaService : IDatabaseSchemaService
{
    private readonly RelationalDatabaseAPI _api;
    private readonly string _sqlDirectory;

    /// <summary>
    /// Initializes a new instance using a connection string.
    /// </summary>
    public PostgresSchemaService(string connectionString)
        : this(new PostgresConnectionFactory(connectionString))
    {
    }

    /// <summary>
    /// Initializes a new instance using a connection factory.
    /// </summary>
    public PostgresSchemaService(IDbConnectionFactory connectionFactory)
    {
        _api = new RelationalDatabaseAPI(connectionFactory);
        _sqlDirectory = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "SQL");
    }

    /// <inheritdoc />
    public async Task<IEnumerable<TableName>> GetTables()
    {
        string sql = await File.ReadAllTextAsync(Path.Combine(_sqlDirectory, "Postgres_TableNames.sql"));
        return await _api.GetAllAsync<TableName>(sql);
    }

    /// <inheritdoc />
    public async Task<IEnumerable<TableField>> GetTableFields()
    {
        string sql = await File.ReadAllTextAsync(Path.Combine(_sqlDirectory, "Postgres_TableAndFieldInfo.sql"));
        return await _api.GetAllAsync<TableField>(sql);
    }

    /// <inheritdoc />
    public async Task<IEnumerable<TableConstraint>> GetConstraints()
    {
        string sql = await File.ReadAllTextAsync(Path.Combine(_sqlDirectory, "Postgres_Constraints.sql"));
        return await _api.GetAllAsync<TableConstraint>(sql);
    }

    /// <inheritdoc />
    public async Task<IEnumerable<TableForeignKey>> GetForeignKeys()
    {
        string sql = await File.ReadAllTextAsync(Path.Combine(_sqlDirectory, "Postgres_ForeignKeyConstraints.sql"));
        return await _api.GetAllAsync<TableForeignKey>(sql);
    }
}
