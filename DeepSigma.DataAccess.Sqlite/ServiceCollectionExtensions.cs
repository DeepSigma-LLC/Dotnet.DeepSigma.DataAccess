using DeepSigma.DataAccess.Abstraction;
using DeepSigma.DataAccess.RelationalDatabase;
using DeepSigma.DataAccess.Sqlite;
using Microsoft.Data.Sqlite;

// ReSharper disable once CheckNamespace -- intentional, so the extension lights up wherever DI is in scope.
namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Dependency-injection registration helpers for the SQLite provider.
/// </summary>
public static class DeepSigmaSqliteServiceCollectionExtensions
{
    /// <summary>
    /// SQLite DDL for the <c>_migrations</c> tracking table used by <see cref="MigrationRunner"/>.
    /// </summary>
    internal const string CreateMigrationsTableSql =
        "CREATE TABLE IF NOT EXISTS _migrations (Id TEXT NOT NULL PRIMARY KEY, AppliedAtUtc TEXT NOT NULL);";

    /// <summary>
    /// Registers <see cref="SqliteConnectionFactory"/>, <see cref="RelationalDatabaseApi"/>,
    /// <see cref="SqliteSchemaService"/>, and <see cref="MigrationRunner"/> as singletons.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="connectionString">SQLite connection string. Examples: <c>Data Source=app.db</c>,
    /// <c>Data Source=:memory:</c>, <c>Data Source=file:test?mode=memory&amp;cache=shared</c>.</param>
    /// <param name="onConnectionOpened">
    /// Optional callback invoked every time a connection transitions to <c>Open</c>. Use to apply per-connection
    /// PRAGMAs (e.g. <c>foreign_keys = ON</c>, <c>synchronous = NORMAL</c>, <c>busy_timeout = 5000</c>).
    /// </param>
    /// <returns>The same <see cref="IServiceCollection"/> for chaining.</returns>
    public static IServiceCollection AddDeepSigmaSqlite(
        this IServiceCollection services,
        string connectionString,
        Action<SqliteConnection>? onConnectionOpened = null)
    {
        services.AddSingleton<IDbConnectionFactory>(_ => new SqliteConnectionFactory(connectionString, onConnectionOpened));
        services.AddSingleton<RelationalDatabaseApi>();
        services.AddSingleton<IDatabaseSchemaService, SqliteSchemaService>();
        services.AddSingleton(sp => ActivatorUtilities.CreateInstance<MigrationRunner>(sp, CreateMigrationsTableSql));
        return services;
    }
}
