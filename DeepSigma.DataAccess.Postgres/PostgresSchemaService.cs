using DeepSigma.DataAccess.Abstraction;
using DeepSigma.DataAccess.RelationalDatabase;
using Microsoft.Extensions.Logging;

namespace DeepSigma.DataAccess.Postgres;

/// <summary>
/// Retrieves schema information from a PostgreSQL database: tables, fields,
/// constraints, and foreign keys. Defaults to the "public" schema.
/// </summary>
public sealed class PostgresSchemaService : RelationalSchemaServiceBase
{
    /// <summary>Initializes a new instance using a connection string.</summary>
    public PostgresSchemaService(string connectionString, ILogger<PostgresSchemaService>? logger = null)
        : this(new PostgresConnectionFactory(connectionString), logger)
    {
    }

    /// <summary>Initializes a new instance using a connection factory.</summary>
    public PostgresSchemaService(IDbConnectionFactory connectionFactory, ILogger<PostgresSchemaService>? logger = null)
        : base(connectionFactory, filePrefix: "Postgres", logger)
    {
    }
}
