using System.Reflection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Npgsql;

namespace DeepSigma.DataAccess.Postgres;

/// <summary>
/// High-throughput bulk-loader for PostgreSQL, backed by the <c>COPY … FROM STDIN BINARY</c> protocol
/// (exposed by Npgsql as <see cref="NpgsqlBinaryImporter"/>).
/// <para>
/// This class deliberately sits outside the standard CRUD surface provided by
/// <c>DeepSigma.DataAccess.RelationalDatabase.RelationalDatabaseApi</c>: <c>COPY</c> uses a different
/// wire protocol from <c>INSERT</c>, has different failure semantics, and is Postgres-specific.
/// Exposing it through the portable <c>InsertAllAsync</c> API would either misrepresent what is
/// happening or hide performance characteristics callers need to know about.
/// </para>
/// </summary>
/// <remarks>
/// <para><b>Why this exists.</b> <c>InsertAllAsync</c> sends one <c>INSERT</c> per row: round-trip,
/// parse, plan, WAL log, index update. For ingestion workloads (ETL, migrations, telemetry backfills)
/// this is often <i>10–100× slower</i> than <c>COPY</c>, which streams rows in Postgres's binary
/// format with minimal per-row overhead. If you are loading more than a few thousand rows in one
/// operation, this is the right tool.</para>
/// <para><b>Why it deviates from the standard API.</b>
/// <list type="bullet">
/// <item><description><b>No per-row errors.</b> A constraint violation aborts the entire COPY — there is no
/// partial-success state. <c>InsertAllAsync</c> reports affected rows; this returns a total count, with errors as exceptions.</description></item>
/// <item><description><b>No generated identity values returned.</b> Rows are streamed in; the server-assigned
/// <c>SERIAL</c> / <c>IDENTITY</c> values are not handed back. <c>COPY</c> into a staging table first and then
/// <c>INSERT … SELECT … RETURNING id</c> if you need them.</description></item>
/// <item><description><b>POCO → column mapping is by name.</b> Every public readable property on <typeparamref name="T"/>
/// is mapped to a destination column with the same name (case-sensitive — Postgres folds unquoted identifiers
/// to lowercase, so column names are emitted quoted). Use a DTO that mirrors the destination table if you need a different shape.</description></item>
/// <item><description><b>Type inference.</b> Each value is passed to <c>NpgsqlBinaryImporter.WriteAsync(value)</c>
/// which infers the Postgres type from the CLR type. This works for most BCL types (numerics, strings, dates,
/// guids, byte arrays, enums). For unusual types you may need to switch to row-by-row <c>INSERT</c>.</description></item>
/// </list>
/// </para>
/// <para><b>When to use.</b> Ingestion-shaped workloads — millions of rows, nightly loads, data migration,
/// telemetry backfills. For small inserts inside an OLTP flow, stick with <c>RelationalDatabaseApi.InsertAsync</c>
/// (which gives you per-row results and generated IDs).</para>
/// <para><b>Memory.</b> Rows are streamed via Npgsql's binary importer — the full sequence is <i>not</i>
/// materialized in memory before sending. Pass any deferred <see cref="IEnumerable{T}"/> freely.</para>
/// </remarks>
public sealed class PostgresBulkCopier
{
    private readonly string _connectionString;
    private readonly ILogger<PostgresBulkCopier> _logger;

    /// <summary>
    /// Initializes a new instance of <see cref="PostgresBulkCopier"/>.
    /// </summary>
    /// <param name="connectionString">PostgreSQL connection string. Same shape as used by other providers in this family.</param>
    /// <param name="logger">Optional logger; defaults to <see cref="NullLogger{T}.Instance"/>.</param>
    public PostgresBulkCopier(string connectionString, ILogger<PostgresBulkCopier>? logger = null)
    {
        _connectionString = connectionString;
        _logger = logger ?? NullLogger<PostgresBulkCopier>.Instance;
    }

    /// <summary>
    /// Bulk-copies <paramref name="rows"/> into <paramref name="destinationTable"/> using Postgres binary <c>COPY</c>.
    /// </summary>
    /// <typeparam name="T">A POCO whose public readable property names match the destination column names.</typeparam>
    /// <param name="destinationTable">Destination table name. Schema-qualify if not in <c>public</c> (e.g. <c>analytics.events</c>).</param>
    /// <param name="rows">Rows to copy. Streamed — not buffered in memory.</param>
    /// <param name="cancellationToken">Cancellation token honoured across connection open, row write, and complete.</param>
    /// <returns>The number of rows successfully copied (from <see cref="NpgsqlBinaryImporter.CompleteAsync"/>).</returns>
    /// <exception cref="InvalidOperationException">Thrown if <typeparamref name="T"/> has no public readable properties.</exception>
    /// <exception cref="PostgresException">Thrown on connection / constraint / type-mismatch errors.</exception>
    public async Task<long> BulkCopyAsync<T>(
        string destinationTable,
        IEnumerable<T> rows,
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

        string columnList = string.Join(", ", properties.Select(p => $"\"{p.Name}\""));
        string copyCommand = $"COPY {destinationTable} ({columnList}) FROM STDIN (FORMAT BINARY)";

        _logger.LogDebug(
            "Postgres binary COPY starting: destination={Table}, columns={Columns}",
            destinationTable, properties.Length);

        await using var connection = new NpgsqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);

        await using var writer = await connection.BeginBinaryImportAsync(copyCommand, cancellationToken);

        foreach (T row in rows)
        {
            await writer.StartRowAsync(cancellationToken);
            foreach (PropertyInfo prop in properties)
            {
                object? value = prop.GetValue(row);
                if (value is null)
                {
                    await writer.WriteNullAsync(cancellationToken);
                }
                else
                {
                    await writer.WriteAsync(value, cancellationToken);
                }
            }
        }

        ulong copied = await writer.CompleteAsync(cancellationToken);
        _logger.LogDebug("Postgres binary COPY complete: {Rows} rows copied to {Table}", copied, destinationTable);
        return (long)copied;
    }
}
