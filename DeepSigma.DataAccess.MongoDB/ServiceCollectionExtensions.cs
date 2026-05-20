using DeepSigma.DataAccess.MongoDB;

// ReSharper disable once CheckNamespace -- intentional, so the extension lights up wherever DI is in scope.
namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Dependency-injection registration helpers for the MongoDB provider.
/// </summary>
public static class DeepSigmaMongoDbServiceCollectionExtensions
{
    /// <summary>
    /// Registers <see cref="MongoDbApi"/> as a singleton. The underlying <c>MongoClient</c>
    /// is designed to be shared and pools connections internally.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="connectionString">MongoDB connection string.</param>
    /// <returns>The same <see cref="IServiceCollection"/> for chaining.</returns>
    public static IServiceCollection AddDeepSigmaMongoDb(this IServiceCollection services, string connectionString)
    {
        services.AddSingleton(sp => ActivatorUtilities.CreateInstance<MongoDbApi>(sp, connectionString));
        return services;
    }
}
