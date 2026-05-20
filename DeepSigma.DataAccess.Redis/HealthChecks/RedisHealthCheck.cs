using Microsoft.Extensions.Diagnostics.HealthChecks;
using StackExchange.Redis;

namespace DeepSigma.DataAccess.Redis.HealthChecks;

/// <summary>
/// Health check that connects to Redis and runs <c>PING</c>. Verifies the server is reachable
/// and responsive. Creates a throwaway <see cref="ConnectionMultiplexer"/> per check, so the
/// check exercises the full connect-and-handshake path on every invocation.
/// </summary>
internal sealed class RedisHealthCheck : IHealthCheck
{
    private readonly string _connectionString;

    public RedisHealthCheck(string connectionString) => _connectionString = connectionString;

    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            await using var connection = await ConnectionMultiplexer.ConnectAsync(_connectionString);
            await connection.GetDatabase().PingAsync();
            return HealthCheckResult.Healthy();
        }
        catch (Exception ex)
        {
            return new HealthCheckResult(
                context.Registration.FailureStatus,
                "Redis PING failed.",
                ex);
        }
    }
}
