using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace DeepSigma.DataAccess.Sqlite.HealthChecks;

/// <summary>
/// Health check that opens a SQLite connection and runs <c>SELECT 1</c>.
/// Verifies the database file is reachable and the SQLite library can answer.
/// For in-memory databases (<c>Data Source=:memory:</c>) this also confirms the
/// process can allocate a SQLite session — which is essentially always true on
/// a healthy host.
/// </summary>
internal sealed class SqliteHealthCheck : IHealthCheck
{
    private readonly string _connectionString;

    public SqliteHealthCheck(string connectionString) => _connectionString = connectionString;

    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            await using var connection = new SqliteConnection(_connectionString);
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
                "SQLite connection or `SELECT 1` failed.",
                ex);
        }
    }
}
