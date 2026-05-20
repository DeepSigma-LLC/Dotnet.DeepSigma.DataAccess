using DeepSigma.DataAccess.Redis;

// ReSharper disable once CheckNamespace -- intentional, so the extension lights up wherever DI is in scope.
namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Dependency-injection registration helpers for the Redis cache.
/// </summary>
public static class DeepSigmaRedisServiceCollectionExtensions
{
    /// <summary>
    /// Registers <see cref="RedisCacheApi"/> as a singleton so the underlying
    /// <c>ConnectionMultiplexer</c> is shared across the application.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="connectionString">Redis connection string.</param>
    /// <param name="instanceName">Instance name (currently stored but not applied to keys).</param>
    /// <returns>The same <see cref="IServiceCollection"/> for chaining.</returns>
    public static IServiceCollection AddDeepSigmaRedis(this IServiceCollection services, string connectionString, string instanceName)
    {
        services.AddSingleton(sp => ActivatorUtilities.CreateInstance<RedisCacheApi>(sp, connectionString, instanceName));
        return services;
    }
}
