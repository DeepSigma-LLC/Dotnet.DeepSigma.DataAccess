using DeepSigma.DataAccess.Abstraction;
using DeepSigma.DataAccess.RelationalDatabase;
using DeepSigma.DataAccess.SqlServer;
using Microsoft.Data.SqlClient;

// ReSharper disable once CheckNamespace -- intentional, so the extension lights up wherever DI is in scope.
namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Dependency-injection registration helpers for the SQL Server provider.
/// </summary>
public static class DeepSigmaSqlServerServiceCollectionExtensions
{
    /// <summary>
    /// SQL Server DDL for the <c>_migrations</c> tracking table used by <see cref="MigrationRunner"/>.
    /// SQL Server has no <c>CREATE TABLE IF NOT EXISTS</c>, so the existence check is explicit.
    /// </summary>
    internal const string CreateMigrationsTableSql = """
        IF NOT EXISTS (SELECT 1 FROM sys.objects WHERE type = 'U' AND name = '_migrations')
        BEGIN
            CREATE TABLE _migrations (
                Id NVARCHAR(255) NOT NULL PRIMARY KEY,
                AppliedAtUtc DATETIME2 NOT NULL
            );
        END
        """;

    /// <summary>
    /// Registers <see cref="SqlServerConnectionFactory"/>, <see cref="RelationalDatabaseApi"/>,
    /// <see cref="SqlServerSchemaService"/>, <see cref="SqlServerBulkCopier"/>, and
    /// <see cref="MigrationRunner"/> as singletons.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="connectionString">SQL Server connection string.</param>
    /// <param name="onConnectionOpened">
    /// Optional callback invoked every time a connection transitions to <c>Open</c>. Use to run per-connection
    /// SET statements (e.g. <c>SET ANSI_NULLS ON</c>, <c>SET ARITHABORT ON</c>).
    /// </param>
    /// <returns>The same <see cref="IServiceCollection"/> for chaining.</returns>
    public static IServiceCollection AddDeepSigmaSqlServer(
        this IServiceCollection services,
        string connectionString,
        Action<SqlConnection>? onConnectionOpened = null)
    {
        services.AddSingleton<IDbConnectionFactory>(_ => new SqlServerConnectionFactory(connectionString, onConnectionOpened));
        services.AddSingleton<RelationalDatabaseApi>();
        services.AddSingleton<IDatabaseSchemaService, SqlServerSchemaService>();
        services.AddSingleton(sp => ActivatorUtilities.CreateInstance<SqlServerBulkCopier>(sp, connectionString));
        services.AddSingleton(sp => ActivatorUtilities.CreateInstance<MigrationRunner>(sp, CreateMigrationsTableSql));
        return services;
    }
}
