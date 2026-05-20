using DeepSigma.DataAccess.Abstraction;
using DeepSigma.DataAccess.RelationalDatabase;
using DeepSigma.DataAccess.Sqlite;

// ReSharper disable once CheckNamespace -- intentional, so the extension lights up wherever DI is in scope.
namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Dependency-injection registration helpers for the SQLite provider.
/// </summary>
public static class DeepSigmaSqliteServiceCollectionExtensions
{
    /// <summary>
    /// Registers <see cref="SqliteConnectionFactory"/>, <see cref="RelationalDatabaseApi"/>, and
    /// <see cref="SqliteSchemaService"/> as singletons.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="connectionString">SQLite connection string. Examples: <c>Data Source=app.db</c>,
    /// <c>Data Source=:memory:</c>, <c>Data Source=file:test?mode=memory&amp;cache=shared</c>.</param>
    /// <returns>The same <see cref="IServiceCollection"/> for chaining.</returns>
    public static IServiceCollection AddDeepSigmaSqlite(this IServiceCollection services, string connectionString)
    {
        services.AddSingleton<IDbConnectionFactory>(_ => new SqliteConnectionFactory(connectionString));
        services.AddSingleton<RelationalDatabaseApi>();
        services.AddSingleton<IDatabaseSchemaService, SqliteSchemaService>();
        return services;
    }
}
