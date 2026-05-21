using DeepSigma.DataAccess.Abstraction;
using DeepSigma.DataAccess.RelationalDatabase;
using Microsoft.Extensions.Logging;

namespace DeepSigma.DataAccess.SqlServer;

/// <summary>
/// Retrieves schema information from a SQL Server database: tables, fields,
/// constraints, and foreign keys.
/// </summary>
public sealed class SqlServerSchemaService : RelationalSchemaServiceBase
{
    /// <summary>Initializes a new instance using a connection string.</summary>
    public SqlServerSchemaService(string connectionString, ILogger<SqlServerSchemaService>? logger = null)
        : this(new SqlServerConnectionFactory(connectionString), logger)
    {
    }

    /// <summary>Initializes a new instance using a connection factory.</summary>
    public SqlServerSchemaService(IDbConnectionFactory connectionFactory, ILogger<SqlServerSchemaService>? logger = null)
        : base(connectionFactory, filePrefix: "SqlServer", logger)
    {
    }
}
