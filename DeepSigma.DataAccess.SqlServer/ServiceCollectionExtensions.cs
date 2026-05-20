using DeepSigma.DataAccess.Abstraction;
using DeepSigma.DataAccess.RelationalDatabase;
using DeepSigma.DataAccess.SqlServer;

// ReSharper disable once CheckNamespace -- intentional, so the extension lights up wherever DI is in scope.
namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Dependency-injection registration helpers for the SQL Server provider.
/// </summary>
public static class DeepSigmaSqlServerServiceCollectionExtensions
{
    /// <summary>
    /// Registers <see cref="SqlServerConnectionFactory"/>, <see cref="RelationalDatabaseApi"/>, and
    /// <see cref="SqlServerSchemaService"/> as singletons.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="connectionString">SQL Server connection string.</param>
    /// <returns>The same <see cref="IServiceCollection"/> for chaining.</returns>
    public static IServiceCollection AddDeepSigmaSqlServer(this IServiceCollection services, string connectionString)
    {
        services.AddSingleton<IDbConnectionFactory>(_ => new SqlServerConnectionFactory(connectionString));
        services.AddSingleton<RelationalDatabaseApi>();
        services.AddSingleton<IDatabaseSchemaService, SqlServerSchemaService>();
        services.AddSingleton(sp => ActivatorUtilities.CreateInstance<SqlServerBulkCopier>(sp, connectionString));
        return services;
    }
}
