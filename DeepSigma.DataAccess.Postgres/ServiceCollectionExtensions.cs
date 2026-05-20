using DeepSigma.DataAccess.Abstraction;
using DeepSigma.DataAccess.Postgres;
using DeepSigma.DataAccess.RelationalDatabase;

// ReSharper disable once CheckNamespace -- intentional, so the extension lights up wherever DI is in scope.
namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Dependency-injection registration helpers for the PostgreSQL provider.
/// </summary>
public static class DeepSigmaPostgresServiceCollectionExtensions
{
    /// <summary>
    /// Registers <see cref="PostgresConnectionFactory"/>, <see cref="RelationalDatabaseApi"/>, and
    /// <see cref="PostgresSchemaService"/> as singletons.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="connectionString">PostgreSQL connection string.</param>
    /// <returns>The same <see cref="IServiceCollection"/> for chaining.</returns>
    public static IServiceCollection AddDeepSigmaPostgres(this IServiceCollection services, string connectionString)
    {
        services.AddSingleton<IDbConnectionFactory>(_ => new PostgresConnectionFactory(connectionString));
        services.AddSingleton<RelationalDatabaseApi>();
        services.AddSingleton<IDatabaseSchemaService, PostgresSchemaService>();
        services.AddSingleton(sp => ActivatorUtilities.CreateInstance<PostgresBulkCopier>(sp, connectionString));
        return services;
    }
}
