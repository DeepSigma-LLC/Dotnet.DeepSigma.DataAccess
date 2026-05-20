using Azure;
using Azure.Storage.Blobs;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace DeepSigma.DataAccess.AzureBlobStorage.HealthChecks;

/// <summary>
/// Health check that calls <see cref="BlobContainerClient.ExistsAsync"/> against the configured
/// container. Reports the storage account as healthy only when it can be reached AND the named
/// container is present — a missing container is treated as a failure since the rest of the API
/// surface is bound to it.
/// </summary>
internal sealed class BlobStorageHealthCheck : IHealthCheck
{
    private readonly string _connectionString;
    private readonly string _containerName;

    public BlobStorageHealthCheck(string connectionString, string containerName)
    {
        _connectionString = connectionString;
        _containerName = containerName;
    }

    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var serviceClient = new BlobServiceClient(_connectionString);
            var containerClient = serviceClient.GetBlobContainerClient(_containerName);
            Response<bool> response = await containerClient.ExistsAsync(cancellationToken);
            if (!response.Value)
            {
                return new HealthCheckResult(
                    context.Registration.FailureStatus,
                    $"Container '{_containerName}' does not exist on the storage account.");
            }
            return HealthCheckResult.Healthy();
        }
        catch (Exception ex)
        {
            return new HealthCheckResult(
                context.Registration.FailureStatus,
                "Azure Blob Storage container check failed.",
                ex);
        }
    }
}
