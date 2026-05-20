using DeepSigma.DataAccess.Cosmos.HealthChecks;
using Microsoft.Extensions.Diagnostics.HealthChecks;

// ReSharper disable once CheckNamespace -- intentional, so the extension lights up wherever DI is in scope.
namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Health-check registration helpers for Azure Cosmos DB.
/// </summary>
public static class DeepSigmaCosmosHealthCheckExtensions
{
    /// <summary>
    /// Registers a Cosmos DB health check that calls <c>ReadAccountAsync</c>.
    /// </summary>
    /// <param name="builder">The <see cref="IHealthChecksBuilder"/>.</param>
    /// <param name="endpointUri">Cosmos DB endpoint URI.</param>
    /// <param name="apiKey">Cosmos DB primary key.</param>
    /// <param name="name">The registration name. Defaults to <c>"deepsigma_cosmos"</c>.</param>
    /// <param name="failureStatus">Status reported on failure. Defaults to <see cref="HealthStatus.Unhealthy"/>.</param>
    /// <param name="tags">Optional tags (e.g. <c>"readiness"</c>).</param>
    /// <param name="timeout">Optional per-check timeout.</param>
    /// <returns>The same <see cref="IHealthChecksBuilder"/> for chaining.</returns>
    public static IHealthChecksBuilder AddDeepSigmaCosmos(
        this IHealthChecksBuilder builder,
        string endpointUri,
        string apiKey,
        string name = "deepsigma_cosmos",
        HealthStatus? failureStatus = null,
        IEnumerable<string>? tags = null,
        TimeSpan? timeout = null)
        => builder.Add(new HealthCheckRegistration(
            name,
            _ => new CosmosHealthCheck(endpointUri, apiKey),
            failureStatus,
            tags,
            timeout));
}
