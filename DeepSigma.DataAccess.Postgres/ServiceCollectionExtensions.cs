using DeepSigma.DataAccess.Abstraction;
using DeepSigma.DataAccess.Postgres;
using DeepSigma.DataAccess.RelationalDatabase;
using Npgsql;

// ReSharper disable once CheckNamespace -- intentional, so the extension lights up wherever DI is in scope.
namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Dependency-injection registration helpers for the PostgreSQL provider.
/// </summary>
public static class DeepSigmaPostgresServiceCollectionExtensions
{
    /// <summary>
    /// Postgres DDL for the <c>_migrations</c> tracking table used by <see cref="MigrationRunner"/>.
    /// Identifiers are intentionally unquoted so Postgres folds them to lowercase — matching the unquoted
    /// column references in <see cref="MigrationRunner"/>'s portable SQL.
    /// </summary>
    internal const string CreateMigrationsTableSql =
        "CREATE TABLE IF NOT EXISTS _migrations (Id TEXT NOT NULL PRIMARY KEY, AppliedAtUtc TIMESTAMPTZ NOT NULL);";

    /// <summary>
    /// Registers <see cref="PostgresConnectionFactory"/>, <see cref="RelationalDatabaseApi"/>,
    /// <see cref="PostgresSchemaService"/>, <see cref="PostgresBulkCopier"/>, and
    /// <see cref="MigrationRunner"/> as singletons.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="connectionString">PostgreSQL connection string.</param>
    /// <param name="configureDataSource">
    /// Optional callback to customize the underlying <see cref="NpgsqlDataSource"/> via
    /// <see cref="NpgsqlDataSourceBuilder"/>. Use for custom type handlers, enum / composite mapping,
    /// password providers, per-source logging, etc.
    /// </param>
    /// <param name="onConnectionOpened">
    /// Optional callback invoked every time a connection transitions to <c>Open</c>. Use for per-connection
    /// SET statements (<c>SET search_path</c>, <c>SET statement_timeout</c>, etc.).
    /// </param>
    /// <returns>The same <see cref="IServiceCollection"/> for chaining.</returns>
    public static IServiceCollection AddDeepSigmaPostgres(
        this IServiceCollection services,
        string connectionString,
        Action<NpgsqlDataSourceBuilder>? configureDataSource = null,
        Action<NpgsqlConnection>? onConnectionOpened = null)
    {
        services.AddSingleton<IDbConnectionFactory>(_ =>
        {
            if (configureDataSource is null)
            {
                return new PostgresConnectionFactory(connectionString, onConnectionOpened);
            }
            var builder = new NpgsqlDataSourceBuilder(connectionString);
            configureDataSource(builder);
            NpgsqlDataSource dataSource = builder.Build();
            // The factory owns this data source (built here, not supplied by the caller),
            // so disposal flows through PostgresConnectionFactory.Dispose() → NpgsqlDataSource.Dispose().
            return new PostgresConnectionFactory(dataSource, ownsDataSource: true, onConnectionOpened);
        });
        services.AddSingleton<RelationalDatabaseApi>();
        services.AddSingleton<IDatabaseSchemaService, PostgresSchemaService>();
        services.AddSingleton(sp => ActivatorUtilities.CreateInstance<PostgresBulkCopier>(sp, connectionString));
        services.AddSingleton(sp => ActivatorUtilities.CreateInstance<MigrationRunner>(sp, CreateMigrationsTableSql));
        return services;
    }
}
