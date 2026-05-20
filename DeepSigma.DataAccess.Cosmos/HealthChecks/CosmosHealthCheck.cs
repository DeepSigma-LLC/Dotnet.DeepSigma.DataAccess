using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace DeepSigma.DataAccess.Cosmos.HealthChecks;

/// <summary>
/// Health check that calls <see cref="CosmosClient.ReadAccountAsync"/> — a lightweight metadata
/// read that does not require any specific database or container to exist. Verifies the
/// endpoint URI, the API key, and basic network connectivity.
/// </summary>
internal sealed class CosmosHealthCheck : IHealthCheck
{
    private readonly string _endpointUri;
    private readonly string _apiKey;

    public CosmosHealthCheck(string endpointUri, string apiKey)
    {
        _endpointUri = endpointUri;
        _apiKey = apiKey;
    }

    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            // ReadAccountAsync has no CancellationToken overload in the v3 SDK;
            // honour the caller's token cooperatively before the request.
            cancellationToken.ThrowIfCancellationRequested();
            using var client = new CosmosClient(_endpointUri, _apiKey);
            await client.ReadAccountAsync();
            return HealthCheckResult.Healthy();
        }
        catch (Exception ex)
        {
            return new HealthCheckResult(
                context.Registration.FailureStatus,
                "Cosmos DB account read failed.",
                ex);
        }
    }
}
