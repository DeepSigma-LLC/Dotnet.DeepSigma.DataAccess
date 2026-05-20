using DeepSigma.DataAccess.SqlServer.HealthChecks;
using Microsoft.Extensions.Diagnostics.HealthChecks;

// ReSharper disable once CheckNamespace -- intentional, so the extension lights up wherever DI is in scope.
namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Health-check registration helpers for SQL Server.
/// </summary>
public static class DeepSigmaSqlServerHealthCheckExtensions
{
    /// <summary>
    /// Registers a SQL Server health check that opens a connection and runs <c>SELECT 1</c>.
    /// </summary>
    /// <param name="builder">The <see cref="IHealthChecksBuilder"/>.</param>
    /// <param name="connectionString">SQL Server connection string.</param>
    /// <param name="name">The registration name. Defaults to <c>"deepsigma_sqlserver"</c>.</param>
    /// <param name="failureStatus">Status reported on failure. Defaults to <see cref="HealthStatus.Unhealthy"/>.</param>
    /// <param name="tags">Optional tags (e.g. <c>"readiness"</c>).</param>
    /// <param name="timeout">Optional per-check timeout.</param>
    /// <returns>The same <see cref="IHealthChecksBuilder"/> for chaining.</returns>
    public static IHealthChecksBuilder AddDeepSigmaSqlServer(
        this IHealthChecksBuilder builder,
        string connectionString,
        string name = "deepsigma_sqlserver",
        HealthStatus? failureStatus = null,
        IEnumerable<string>? tags = null,
        TimeSpan? timeout = null)
        => builder.Add(new HealthCheckRegistration(
            name,
            _ => new SqlServerHealthCheck(connectionString),
            failureStatus,
            tags,
            timeout));
}
