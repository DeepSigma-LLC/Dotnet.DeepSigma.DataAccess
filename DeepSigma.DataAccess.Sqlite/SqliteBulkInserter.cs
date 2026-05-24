using System.Reflection;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace DeepSigma.DataAccess.Sqlite;

/// <summary>
/// High-throughput bulk-loader for SQLite. SQLite has no wire-level bulk
/// protocol equivalent to SQL Server's <c>SqlBulkCopy</c> or Postgres's
/// <c>COPY … FROM STDIN BINARY</c>; the throughput win on this provider
/// comes from <b>(1)</b> wrapping the entire batch in a single transaction
/// (avoids per-INSERT fsync) and <b>(2)</b> reusing a prepared statement
/// across rows (skips per-row parse + plan). Combined this is often
/// <i>50–100×</i> faster than naïve per-row INSERTs on small batches and
/// <i>5–20×</i> on large batches.
/// </summary>
/// <remarks>
/// <para><b>Why this exists.</b> The naïve per-row pattern
/// (<c>foreach { connection.Execute("INSERT …") }</c>) opens an implicit
/// transaction per call, fsyncs the WAL, and re-parses / re-plans the
/// statement every time. Both costs vanish under one explicit transaction
/// and one prepared command.</para>
/// <para><b>Why it deviates from the standard CRUD API.</b>
/// <list type="bullet">
/// <item><description><b>All-or-nothing.</b> The entire batch is one transaction.
/// A constraint violation on any row rolls back every row in the batch.</description></item>
/// <item><description><b>No generated identity values returned.</b> Use a follow-up
/// <c>SELECT last_insert_rowid()</c> or <c>INSERT … RETURNING</c> if you need them.</description></item>
/// <item><description><b>POCO → column mapping is by name.</b> Every public readable
/// property is mapped to a parameter (<c>@PropertyName</c>) and a column of the same name.</description></item>
/// <item><description><b>Uses the supplied <see cref="SqliteConnectionFactory"/>.</b>
/// Consumers wiring per-connection setup (e.g. loading the <c>vec0</c> extension)
/// configure it once on the factory; the bulk inserter inherits it.</description></item>
/// </list>
/// </para>
/// <para><b>When to use.</b> Any batch ≥ ~10 rows. Single-row inserts go through
/// the standard <c>RelationalDatabaseApi.InsertAsync</c>.</para>
/// </remarks>
public sealed class SqliteBulkInserter
{
    private readonly SqliteConnectionFactory _factory;
    private readonly ILogger<SqliteBulkInserter> _logger;

    /// <summary>Initializes a new instance of <see cref="SqliteBulkInserter"/>.</summary>
    /// <param name="factory">Connection factory whose <c>onConnectionOpened</c> callback (if any) is honoured for per-connection setup (PRAGMAs, extension loads).</param>
    /// <param name="logger">Optional logger; defaults to <see cref="NullLogger{T}.Instance"/>.</param>
    public SqliteBulkInserter(SqliteConnectionFactory factory, ILogger<SqliteBulkInserter>? logger = null)
    {
        ArgumentNullException.ThrowIfNull(factory);
        _factory = factory;
        _logger = logger ?? NullLogger<SqliteBulkInserter>.Instance;
    }

    /// <summary>
    /// Bulk-inserts <paramref name="rows"/> into <paramref name="destinationTable"/>
    /// inside a single transaction with a prepared statement.
    /// </summary>
    /// <typeparam name="T">A POCO whose public readable property names match the destination column names.</typeparam>
    /// <param name="destinationTable">Destination table name (single identifier; emitted as-is).</param>
    /// <param name="rows">Rows to insert. Streamed — not buffered in memory.</param>
    /// <param name="conflictBehavior">How to handle PK / unique-index conflicts. Default <see cref="SqliteConflictBehavior.Abort"/>.</param>
    /// <param name="cancellationToken">Honoured between rows.</param>
    /// <returns>The number of rows inserted.</returns>
    /// <exception cref="InvalidOperationException">Thrown if <typeparamref name="T"/> has no public readable properties.</exception>
    /// <exception cref="SqliteException">Thrown on connection / constraint / type-mismatch errors.</exception>
    public async Task<long> BulkInsertAsync<T>(
        string destinationTable,
        IEnumerable<T> rows,
        SqliteConflictBehavior conflictBehavior = SqliteConflictBehavior.Abort,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(destinationTable);
        ArgumentNullException.ThrowIfNull(rows);

        var properties = typeof(T)
            .GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(p => p.CanRead)
            .ToArray();

        if (properties.Length == 0)
        {
            throw new InvalidOperationException(
                $"Type {typeof(T).Name} has no public readable properties to insert. " +
                "Bulk-insert needs at least one property to map to a destination column.");
        }

        var columns = string.Join(", ", properties.Select(p => p.Name));
        var parameters = string.Join(", ", properties.Select(p => "@" + p.Name));
        var verb = conflictBehavior switch
        {
            SqliteConflictBehavior.Replace => "INSERT OR REPLACE",
            SqliteConflictBehavior.Ignore => "INSERT OR IGNORE",
            _ => "INSERT",
        };
        var sql = $"{verb} INTO {destinationTable} ({columns}) VALUES ({parameters})";

        _logger.LogDebug(
            "SqliteBulkInsert starting: destination={Table}, columns={Columns}, conflict={Conflict}",
            destinationTable, properties.Length, conflictBehavior);

        await using var connection = (SqliteConnection)_factory.Create();
        await connection.OpenAsync(cancellationToken);   // fires StateChange → onConnectionOpened callback (vec0 load etc.)
        await using var transaction = (SqliteTransaction)await connection.BeginTransactionAsync(cancellationToken);

        long count = await BulkInsertCoreAsync(connection, transaction, destinationTable, rows, properties, conflictBehavior, cancellationToken);

        await transaction.CommitAsync(cancellationToken);
        _logger.LogDebug("SqliteBulkInsert complete: {Rows} rows inserted into {Table}", count, destinationTable);
        return count;
    }

    /// <summary>
    /// Bulk-insert overload taking a caller-owned open connection and
    /// transaction. Use when several bulk-inserts must execute inside the
    /// same transaction — e.g. inserting into a paired vec0 virtual table
    /// + companion content table where the two writes must be atomic.
    /// The caller owns the transaction (commit / rollback). The
    /// connection's open StateChange callback (if any) has already fired
    /// by the time the caller passes it in.
    /// </summary>
    public async Task<long> BulkInsertAsync<T>(
        SqliteConnection connection,
        SqliteTransaction transaction,
        string destinationTable,
        IEnumerable<T> rows,
        SqliteConflictBehavior conflictBehavior = SqliteConflictBehavior.Abort,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(connection);
        ArgumentNullException.ThrowIfNull(transaction);
        ArgumentException.ThrowIfNullOrWhiteSpace(destinationTable);
        ArgumentNullException.ThrowIfNull(rows);

        var properties = typeof(T)
            .GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(p => p.CanRead)
            .ToArray();
        if (properties.Length == 0)
        {
            throw new InvalidOperationException(
                $"Type {typeof(T).Name} has no public readable properties to insert.");
        }

        _logger.LogDebug(
            "SqliteBulkInsert (caller-owned tx) starting: destination={Table}, columns={Columns}, conflict={Conflict}",
            destinationTable, properties.Length, conflictBehavior);

        return await BulkInsertCoreAsync(connection, transaction, destinationTable, rows, properties, conflictBehavior, cancellationToken);
    }

    private static async Task<long> BulkInsertCoreAsync<T>(
        SqliteConnection connection,
        SqliteTransaction transaction,
        string destinationTable,
        IEnumerable<T> rows,
        PropertyInfo[] properties,
        SqliteConflictBehavior conflictBehavior,
        CancellationToken cancellationToken)
    {
        var columns = string.Join(", ", properties.Select(p => p.Name));
        var parameters = string.Join(", ", properties.Select(p => "@" + p.Name));
        var verb = conflictBehavior switch
        {
            SqliteConflictBehavior.Replace => "INSERT OR REPLACE",
            SqliteConflictBehavior.Ignore => "INSERT OR IGNORE",
            _ => "INSERT",
        };
        var sql = $"{verb} INTO {destinationTable} ({columns}) VALUES ({parameters})";

        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = sql;

        // Build parameter set once; we'll mutate Value per row.
        var sqliteParams = new SqliteParameter[properties.Length];
        for (var i = 0; i < properties.Length; i++)
        {
            sqliteParams[i] = command.CreateParameter();
            sqliteParams[i].ParameterName = "@" + properties[i].Name;
            command.Parameters.Add(sqliteParams[i]);
        }
        command.Prepare();   // SQLite caches the parsed/planned statement for re-execution

        long count = 0;
        foreach (var row in rows)
        {
            cancellationToken.ThrowIfCancellationRequested();
            for (var i = 0; i < properties.Length; i++)
            {
                sqliteParams[i].Value = properties[i].GetValue(row) ?? DBNull.Value;
            }
            await command.ExecuteNonQueryAsync(cancellationToken);
            count++;
        }
        return count;
    }
}

/// <summary>Maps to SQLite's <c>ON CONFLICT</c> clauses for the bulk inserter.</summary>
public enum SqliteConflictBehavior
{
    /// <summary>Default — fail and roll back the entire batch on the first conflict.</summary>
    Abort = 0,

    /// <summary>Replace the existing row on conflict (<c>INSERT OR REPLACE</c>). Use for upsert semantics keyed on PK / unique index.</summary>
    Replace = 1,

    /// <summary>Skip the conflicting row and continue (<c>INSERT OR IGNORE</c>). Use for "first writer wins" idempotent inserts.</summary>
    Ignore = 2,
}
