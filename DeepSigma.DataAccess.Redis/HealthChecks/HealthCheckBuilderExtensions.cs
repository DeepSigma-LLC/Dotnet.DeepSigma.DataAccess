using DeepSigma.DataAccess.Redis.HealthChecks;
using Microsoft.Extensions.Diagnostics.HealthChecks;

// ReSharper disable once CheckNamespace -- intentional, so the extension lights up wherever DI is in scope.
namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Health-check registration helpers for Redis.
/// </summary>
public static class DeepSigmaRedisHealthCheckExtensions
{
    /// <summary>
    /// Registers a Redis health check that connects and runs <c>PING</c>.
    /// </summary>
    /// <param name="builder">The <see cref="IHealthChecksBuilder"/>.</param>
    /// <param name="connectionString">Redis connection string.</param>
    /// <param name="name">The registration name. Defaults to <c>"deepsigma_redis"</c>.</param>
    /// <param name="failureStatus">Status reported on failure. Defaults to <see cref="HealthStatus.Unhealthy"/>.</param>
    /// <param name="tags">Optional tags (e.g. <c>"readiness"</c>).</param>
    /// <param name="timeout">Optional per-check timeout.</param>
    /// <returns>The same <see cref="IHealthChecksBuilder"/> for chaining.</returns>
    public static IHealthChecksBuilder AddDeepSigmaRedis(
        this IHealthChecksBuilder builder,
        string connectionString,
        string name = "deepsigma_redis",
        HealthStatus? failureStatus = null,
        IEnumerable<string>? tags = null,
        TimeSpan? timeout = null)
        => builder.Add(new HealthCheckRegistration(
            name,
            _ => new RedisHealthCheck(connectionString),
            failureStatus,
            tags,
            timeout));
}
