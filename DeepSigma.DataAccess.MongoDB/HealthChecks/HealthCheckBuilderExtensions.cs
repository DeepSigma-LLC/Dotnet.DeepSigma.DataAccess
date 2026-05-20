using DeepSigma.DataAccess.MongoDB.HealthChecks;
using Microsoft.Extensions.Diagnostics.HealthChecks;

// ReSharper disable once CheckNamespace -- intentional, so the extension lights up wherever DI is in scope.
namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Health-check registration helpers for MongoDB.
/// </summary>
public static class DeepSigmaMongoDbHealthCheckExtensions
{
    /// <summary>
    /// Registers a MongoDB health check that runs the <c>{ ping: 1 }</c> admin command.
    /// </summary>
    /// <param name="builder">The <see cref="IHealthChecksBuilder"/>.</param>
    /// <param name="connectionString">MongoDB connection string.</param>
    /// <param name="name">The registration name. Defaults to <c>"deepsigma_mongodb"</c>.</param>
    /// <param name="failureStatus">Status reported on failure. Defaults to <see cref="HealthStatus.Unhealthy"/>.</param>
    /// <param name="tags">Optional tags (e.g. <c>"readiness"</c>).</param>
    /// <param name="timeout">Optional per-check timeout.</param>
    /// <returns>The same <see cref="IHealthChecksBuilder"/> for chaining.</returns>
    public static IHealthChecksBuilder AddDeepSigmaMongoDb(
        this IHealthChecksBuilder builder,
        string connectionString,
        string name = "deepsigma_mongodb",
        HealthStatus? failureStatus = null,
        IEnumerable<string>? tags = null,
        TimeSpan? timeout = null)
        => builder.Add(new HealthCheckRegistration(
            name,
            _ => new MongoDbHealthCheck(connectionString),
            failureStatus,
            tags,
            timeout));
}
