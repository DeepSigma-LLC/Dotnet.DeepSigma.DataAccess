using Microsoft.Extensions.Diagnostics.HealthChecks;
using MongoDB.Bson;
using MongoDB.Driver;

namespace DeepSigma.DataAccess.MongoDB.HealthChecks;

/// <summary>
/// Health check that runs the MongoDB <c>{ ping: 1 }</c> administrative command against the
/// <c>admin</c> database. Verifies the server is reachable and answering — does not require
/// any specific application database or collection to exist.
/// </summary>
internal sealed class MongoDbHealthCheck : IHealthCheck
{
    private readonly string _connectionString;

    public MongoDbHealthCheck(string connectionString) => _connectionString = connectionString;

    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var client = new MongoClient(_connectionString);
            var database = client.GetDatabase("admin");
            await database.RunCommandAsync<BsonDocument>(
                new BsonDocument("ping", 1),
                cancellationToken: cancellationToken);
            return HealthCheckResult.Healthy();
        }
        catch (Exception ex)
        {
            return new HealthCheckResult(
                context.Registration.FailureStatus,
                "MongoDB ping failed.",
                ex);
        }
    }
}
