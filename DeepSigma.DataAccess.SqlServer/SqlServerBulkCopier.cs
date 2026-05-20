using System.Reflection;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace DeepSigma.DataAccess.SqlServer;

/// <summary>
/// High-throughput bulk-loader for SQL Server, backed by <see cref="SqlBulkCopy"/>.
/// <para>
/// This class deliberately sits outside the standard CRUD surface provided by
/// <c>DeepSigma.DataAccess.RelationalDatabase.RelationalDatabaseApi</c>: bulk-copy uses a different
/// wire protocol (TDS bulk load), has different failure semantics, and is provider-specific.
/// Trying to expose it through the portable <c>InsertAllAsync</c> API would either misrepresent
/// what is happening or paper over performance characteristics callers need to know about.
/// </para>
/// </summary>
/// <remarks>
/// <para><b>Why this exists.</b> <c>InsertAllAsync</c> sends one <c>INSERT</c> per row: round-trip,
/// parse, plan, log, index update. For ingestion workloads (ETL, migrations, telemetry backfills)
/// this is often <i>10–100× slower</i> than <see cref="SqlBulkCopy"/>, which streams rows in TDS
/// bulk format, batches index updates, and can be configured for minimal transaction logging.
/// If you are loading more than a few thousand rows in one operation, this is the right tool.
/// </para>
/// <para><b>Why it deviates from the standard API.</b>
/// <list type="bullet">
/// <item><description><b>No per-row errors.</b> A constraint violation rolls back the entire current batch.
/// <c>InsertAllAsync</c> reports affected rows; this returns the total count, with errors as exceptions.</description></item>
/// <item><description><b>No generated identity values returned.</b> Rows are written; the server-assigned
/// <c>IDENTITY</c> values are not handed back. Bulk-copy into a staging table first and then do
/// <c>INSERT … SELECT … OUTPUT INSERTED.Id</c> if you need them.</description></item>
/// <item><description><b>No triggers / constraints by default — depending on options.</b> Pass
/// <see cref="SqlBulkCopyOptions.FireTriggers"/>, <see cref="SqlBulkCopyOptions.CheckConstraints"/>, etc.
/// explicitly if you need them; default behaviour is to skip them for speed.</description></item>
/// <item><description><b>POCO → column mapping is by name.</b> Every public readable property on <typeparamref name="T"/>
/// is mapped to a destination column of the same name. No <c>[Column]</c> attribute support — use a DTO
/// that mirrors the destination table if you need a different shape.</description></item>
/// </list>
/// </para>
/// <para><b>When to use.</b> Ingestion-shaped workloads — millions of rows, nightly loads, data migration,
/// telemetry backfills. For small inserts inside an OLTP flow, stick with <c>RelationalDatabaseApi.InsertAsync</c>
/// (which gives you per-row results and generated IDs).</para>
/// <para><b>Memory.</b> Rows are streamed via an <see cref="ObjectDataReader{T}"/>, so the full sequence
/// is <i>not</i> materialized in memory before sending. Pass any deferred <see cref="IEnumerable{T}"/>
/// (file-reader, paged query, async-generated source) freely.</para>
/// </remarks>
public sealed class SqlServerBulkCopier
{
    private readonly string _connectionString;
    private readonly ILogger<SqlServerBulkCopier> _logger;

    /// <summary>
    /// Initializes a new instance of <see cref="SqlServerBulkCopier"/>.
    /// </summary>
    /// <param name="connectionString">SQL Server connection string. Same shape as used by other providers in this family.</param>
    /// <param name="logger">Optional logger; defaults to <see cref="NullLogger{T}.Instance"/>.</param>
    public SqlServerBulkCopier(string connectionString, ILogger<SqlServerBulkCopier>? logger = null)
    {
        _connectionString = connectionString;
        _logger = logger ?? NullLogger<SqlServerBulkCopier>.Instance;
    }

    /// <summary>
    /// Bulk-copies <paramref name="rows"/> into <paramref name="destinationTable"/> using <see cref="SqlBulkCopy"/>.
    /// </summary>
    /// <typeparam name="T">A POCO whose public readable property names match the destination column names.</typeparam>
    /// <param name="destinationTable">Fully-qualified destination table name (e.g. <c>dbo.MyTable</c>).</param>
    /// <param name="rows">Rows to copy. Streamed — not buffered in memory.</param>
    /// <param name="batchSize">Number of rows per server round-trip. Default 5000. Larger batches are
    /// generally faster but use more memory on the server side; tune for your workload.</param>
    /// <param name="bulkCopyTimeoutSeconds">Per-call timeout. <c>null</c> uses the <see cref="SqlBulkCopy"/> default (30s).</param>
    /// <param name="options">SQL Server bulk-copy options. Default skips triggers, constraint checks, and identity inserts for speed.
    /// Pass <see cref="SqlBulkCopyOptions.KeepIdentity"/> if you are bulk-loading rows with pre-assigned <c>IDENTITY</c> values.</param>
    /// <param name="cancellationToken">Cancellation token honoured by both the connection open and the bulk-copy stream.</param>
    /// <returns>The number of rows successfully copied (from <see cref="SqlBulkCopy.RowsCopied"/>).</returns>
    /// <exception cref="InvalidOperationException">Thrown if <typeparamref name="T"/> has no public readable properties.</exception>
    /// <exception cref="SqlException">Thrown on connection / constraint / type-mismatch errors.</exception>
    public async Task<long> BulkCopyAsync<T>(
        string destinationTable,
        IEnumerable<T> rows,
        int batchSize = 5000,
        int? bulkCopyTimeoutSeconds = null,
        SqlBulkCopyOptions options = SqlBulkCopyOptions.Default,
        CancellationToken cancellationToken = default)
    {
        PropertyInfo[] properties = typeof(T)
            .GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(p => p.CanRead)
            .ToArray();

        if (properties.Length == 0)
        {
            throw new InvalidOperationException(
                $"Type {typeof(T).Name} has no public readable properties to copy. " +
                "Bulk-copy needs at least one property to map to a destination column.");
        }

        _logger.LogDebug(
            "SqlBulkCopy starting: destination={Table}, batchSize={BatchSize}, columns={Columns}",
            destinationTable, batchSize, properties.Length);

        using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);

        using var bulkCopy = new SqlBulkCopy(connection, options, externalTransaction: null)
        {
            DestinationTableName = destinationTable,
            BatchSize = batchSize,
        };
        if (bulkCopyTimeoutSeconds.HasValue)
        {
            bulkCopy.BulkCopyTimeout = bulkCopyTimeoutSeconds.Value;
        }

        foreach (PropertyInfo prop in properties)
        {
            bulkCopy.ColumnMappings.Add(prop.Name, prop.Name);
        }

        using var reader = new ObjectDataReader<T>(rows, properties);
        await bulkCopy.WriteToServerAsync(reader, cancellationToken);

        long copied = bulkCopy.RowsCopied;
        _logger.LogDebug("SqlBulkCopy complete: {Rows} rows copied to {Table}", copied, destinationTable);
        return copied;
    }
}
