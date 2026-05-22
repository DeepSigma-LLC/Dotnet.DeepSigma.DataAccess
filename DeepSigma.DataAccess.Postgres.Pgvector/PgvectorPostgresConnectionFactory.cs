using Npgsql;

namespace DeepSigma.DataAccess.Postgres.Pgvector;

/// <summary>
/// One-line convenience factory for the common case: "give me a
/// <see cref="PostgresConnectionFactory"/> with pgvector wired up and the
/// Pgvector.Dapper type handler registered." Use the extension methods on
/// <see cref="NpgsqlDataSourceBuilder"/> directly when you need finer control.
/// </summary>
public static class PgvectorPostgresConnectionFactory
{
    /// <summary>
    /// Builds a pgvector-enabled <see cref="NpgsqlDataSource"/>, ensures the
    /// Dapper type handler is registered, and returns a
    /// <see cref="PostgresConnectionFactory"/> that owns the data source.
    /// </summary>
    /// <param name="connectionString">Postgres connection string.</param>
    /// <param name="onConnectionOpened">
    /// Optional per-connection initialiser (e.g. <c>SET</c> commands or session
    /// configuration). Mirrors the parameter on
    /// <see cref="PostgresConnectionFactory"/>.
    /// </param>
    public static PostgresConnectionFactory Create(
        string connectionString,
        Action<NpgsqlConnection>? onConnectionOpened = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(connectionString);

        var builder = new NpgsqlDataSourceBuilder(connectionString);
        builder.UsePgvector();
        var dataSource = builder.Build();

        PgvectorDapper.RegisterTypeHandler();

        return new PostgresConnectionFactory(dataSource, ownsDataSource: true, onConnectionOpened);
    }
}
