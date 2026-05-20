using Microsoft.Extensions.Diagnostics.HealthChecks;
using Npgsql;

namespace DeepSigma.DataAccess.Postgres.HealthChecks;

/// <summary>
/// Health check that opens a PostgreSQL connection and runs <c>SELECT 1</c>.
/// Reports the connection or query exception as the failure cause.
/// </summary>
internal sealed class PostgresHealthCheck : IHealthCheck
{
    private readonly string _connectionString;

    public PostgresHealthCheck(string connectionString) => _connectionString = connectionString;

    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            await using var connection = new NpgsqlConnection(_connectionString);
            await connection.OpenAsync(cancellationToken);
            await using var command = connection.CreateCommand();
            command.CommandText = "SELECT 1";
            await command.ExecuteScalarAsync(cancellationToken);
            return HealthCheckResult.Healthy();
        }
        catch (Exception ex)
        {
            return new HealthCheckResult(
                context.Registration.FailureStatus,
                "PostgreSQL connection or `SELECT 1` failed.",
                ex);
        }
    }
}
