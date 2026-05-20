using DeepSigma.DataAccess.AzureBlobStorage.HealthChecks;
using Microsoft.Extensions.Diagnostics.HealthChecks;

// ReSharper disable once CheckNamespace -- intentional, so the extension lights up wherever DI is in scope.
namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Health-check registration helpers for Azure Blob Storage.
/// </summary>
public static class DeepSigmaAzureBlobStorageHealthCheckExtensions
{
    /// <summary>
    /// Registers an Azure Blob Storage health check that verifies the named container exists.
    /// </summary>
    /// <param name="builder">The <see cref="IHealthChecksBuilder"/>.</param>
    /// <param name="connectionString">Azure Storage connection string.</param>
    /// <param name="containerName">Blob container name to probe.</param>
    /// <param name="name">The registration name. Defaults to <c>"deepsigma_azureblob"</c>.</param>
    /// <param name="failureStatus">Status reported on failure. Defaults to <see cref="HealthStatus.Unhealthy"/>.</param>
    /// <param name="tags">Optional tags (e.g. <c>"readiness"</c>).</param>
    /// <param name="timeout">Optional per-check timeout.</param>
    /// <returns>The same <see cref="IHealthChecksBuilder"/> for chaining.</returns>
    public static IHealthChecksBuilder AddDeepSigmaAzureBlobStorage(
        this IHealthChecksBuilder builder,
        string connectionString,
        string containerName,
        string name = "deepsigma_azureblob",
        HealthStatus? failureStatus = null,
        IEnumerable<string>? tags = null,
        TimeSpan? timeout = null)
        => builder.Add(new HealthCheckRegistration(
            name,
            _ => new BlobStorageHealthCheck(connectionString, containerName),
            failureStatus,
            tags,
            timeout));
}
