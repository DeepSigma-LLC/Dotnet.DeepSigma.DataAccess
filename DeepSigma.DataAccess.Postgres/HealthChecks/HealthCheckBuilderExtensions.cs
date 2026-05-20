using DeepSigma.DataAccess.Postgres.HealthChecks;
using Microsoft.Extensions.Diagnostics.HealthChecks;

// ReSharper disable once CheckNamespace -- intentional, so the extension lights up wherever DI is in scope.
namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Health-check registration helpers for PostgreSQL.
/// </summary>
public static class DeepSigmaPostgresHealthCheckExtensions
{
    /// <summary>
    /// Registers a PostgreSQL health check that opens a connection and runs <c>SELECT 1</c>.
    /// </summary>
    /// <param name="builder">The <see cref="IHealthChecksBuilder"/>.</param>
    /// <param name="connectionString">PostgreSQL connection string.</param>
    /// <param name="name">The registration name. Defaults to <c>"deepsigma_postgres"</c>.</param>
    /// <param name="failureStatus">Status reported on failure. Defaults to <see cref="HealthStatus.Unhealthy"/>.</param>
    /// <param name="tags">Optional tags (e.g. <c>"readiness"</c>).</param>
    /// <param name="timeout">Optional per-check timeout.</param>
    /// <returns>The same <see cref="IHealthChecksBuilder"/> for chaining.</returns>
    public static IHealthChecksBuilder AddDeepSigmaPostgres(
        this IHealthChecksBuilder builder,
        string connectionString,
        string name = "deepsigma_postgres",
        HealthStatus? failureStatus = null,
        IEnumerable<string>? tags = null,
        TimeSpan? timeout = null)
        => builder.Add(new HealthCheckRegistration(
            name,
            _ => new PostgresHealthCheck(connectionString),
            failureStatus,
            tags,
            timeout));
}
