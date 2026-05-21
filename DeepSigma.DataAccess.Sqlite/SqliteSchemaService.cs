using DeepSigma.DataAccess.Abstraction;
using DeepSigma.DataAccess.RelationalDatabase;
using Microsoft.Extensions.Logging;

namespace DeepSigma.DataAccess.Sqlite;

/// <summary>
/// Retrieves schema information from a SQLite database: tables, fields, constraints, and foreign keys.
/// All operations report <c>TableSchema = "main"</c> since SQLite has no real schema concept.
/// </summary>
public sealed class SqliteSchemaService : RelationalSchemaServiceBase
{
    /// <summary>Initializes a new instance using a connection string.</summary>
    public SqliteSchemaService(string connectionString, ILogger<SqliteSchemaService>? logger = null)
        : this(new SqliteConnectionFactory(connectionString), logger)
    {
    }

    /// <summary>Initializes a new instance using a connection factory.</summary>
    public SqliteSchemaService(IDbConnectionFactory connectionFactory, ILogger<SqliteSchemaService>? logger = null)
        : base(connectionFactory, filePrefix: "Sqlite", logger)
    {
    }
}
