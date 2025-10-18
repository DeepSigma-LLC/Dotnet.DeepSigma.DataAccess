using DeepSigma.DataAccess.Database.Models;

namespace DeepSigma.DataAccess.Database;

/// <summary>
/// Provides methods to retrieve the schema information of a SQL Server database, including tables, fields, constraints, and foreign keys.
/// </summary>
/// <param name="connection_string"></param>
/// <param name="connection_timeout"></param>
public class SQLServerDatabaseSchemaService(string connection_string, int connection_timeout = 10)
{
    DatabaseAPI API { get; set; } = new(connection_string, RelationalDatabaseType.SQLServer, connection_timeout);

    /// <summary>
    /// Gets the list of tables in the SQL Server database.
    /// </summary>
    /// <returns></returns>
    public async Task<IEnumerable<TableName>> GetTables()
    {
        string sql = File.ReadAllText(@"\SQL\SQLServer_TableNames.sql");
        IEnumerable<TableName> results = await API.GetAllAsync<TableName>(sql);
        return results;
    }

    /// <summary>
    /// Gets the list of fields (columns) in the SQL Server database tables.
    /// </summary>
    /// <returns></returns>
    public async Task<IEnumerable<TableField>> GetTableFields()
    {
        string sql = File.ReadAllText(@"\SQL\SQLServer_TableAndFieldInfo.sql");
        IEnumerable<TableField> results = await API.GetAllAsync<TableField>(sql);
        return results;
    }

    /// <summary>
    /// Gets the list of constraints in the SQL Server database tables.
    /// </summary>
    /// <returns></returns>
    public async Task<IEnumerable<TableConstraint>> GetConstraints()
    {
        string sql = File.ReadAllText(@"\SQL\SQLServer_Constraints.sql");
        IEnumerable<TableConstraint> results = await API.GetAllAsync<TableConstraint>(sql);
        return results;
    }

    /// <summary>
    /// Gets the list of foreign keys in the SQL Server database tables.
    /// </summary>
    /// <returns></returns>
    public async Task<IEnumerable<TableForeignKey>> GetForiegnKeys()
    {
        string sql = File.ReadAllText(@"\SQL\SQLServer_ForeignKeyConstraints.sql");
        IEnumerable<TableForeignKey> results = await API.GetAllAsync<TableForeignKey>(sql);
        return results;
    }
}
