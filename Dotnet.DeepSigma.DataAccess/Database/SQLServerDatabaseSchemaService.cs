using DeepSigma.DataAccess.Database.Models;

namespace DeepSigma.DataAccess.Database;

/// <summary>
/// Provides methods to retrieve the schema information of a SQL Server database, including tables, fields, constraints, and foreign keys.
/// </summary>
/// <param name="connection_string"></param>
/// <param name="connection_timeout"></param>
public class SQLServerDatabaseSchemaService(string connection_string, int connection_timeout = 10)
{
    DatabaseAPI API { get; init; } = new(connection_string, RelationalDatabaseType.SQLServer, connection_timeout);
    string BaseDirectory { get; init; } = AppDomain.CurrentDomain.BaseDirectory;

    /// <summary>
    /// Gets the list of tables in the SQL Server database.
    /// </summary>
    /// <returns></returns>
    public async Task<IEnumerable<TableName>> GetTables()
    {
        string path = Path.Combine(BaseDirectory, "Database" ,"SQL", "SQLServer_TableNames.sql");
        string sql = File.ReadAllText(path);
        IEnumerable<TableName> results = await API.GetAllAsync<TableName>(sql);
        return results;
    }

    /// <summary>
    /// Gets the list of fields (columns) in the SQL Server database tables.
    /// </summary>
    /// <returns></returns>
    public async Task<IEnumerable<TableField>> GetTableFields()
    {
        string path = Path.Combine(BaseDirectory, "Database", "SQL", "SQLServer_TableAndFieldInfo.sql");
        string sql = File.ReadAllText(path);
        IEnumerable<TableField> results = await API.GetAllAsync<TableField>(sql);
        return results;
    }

    /// <summary>
    /// Gets the list of constraints in the SQL Server database tables.
    /// </summary>
    /// <returns></returns>
    public async Task<IEnumerable<TableConstraint>> GetConstraints()
    {
        string path = Path.Combine(BaseDirectory, "Database", "SQL", "SQLServer_Constraints.sql");
        string sql = File.ReadAllText(path);
        IEnumerable<TableConstraint> results = await API.GetAllAsync<TableConstraint>(sql);
        return results;
    }

    /// <summary>
    /// Gets the list of foreign keys in the SQL Server database tables.
    /// </summary>
    /// <returns></returns>
    public async Task<IEnumerable<TableForeignKey>> GetForiegnKeys()
    {
        string path = Path.Combine(BaseDirectory, "Database", "SQL", "SQLServer_ForeignKeyConstraints.sql");
        string sql = File.ReadAllText(path);
        IEnumerable<TableForeignKey> results = await API.GetAllAsync<TableForeignKey>(sql);
        return results;
    }
}
