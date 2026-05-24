using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using StackExchange.Redis;

namespace DeepSigma.DataAccess.Redis;

/// <summary>
/// Generic pipelining helper for Redis bulk writes. Redis has no
/// destination-table concept; the throughput win comes from
/// <see cref="IBatch"/>: queueing N commands without round-tripping per
/// call, then firing them all in one Execute. For HSET / SET / LPUSH /
/// XADD ingestion this is often <i>10–100×</i> faster than the
/// equivalent per-call await loop.
/// </summary>
/// <remarks>
/// <para><b>Why this exists.</b> The naïve per-call pattern
/// (<c>foreach { await db.HashSetAsync(...) }</c>) round-trips per item.
/// <see cref="IBatch"/> queues commands locally; <see cref="IBatch.Execute"/>
/// sends them in one go and the awaited <c>Task</c> handles complete as
/// responses come back. Same protocol, fewer round-trips.</para>
/// <para><b>Generic by design.</b> The caller supplies a per-item lambda
/// that queues whatever commands are needed (HSET, EXPIRE, INDEX, …) and
/// reports any returned <c>Task</c>s to a collector. This keeps the
/// helper one type instead of N domain-specific uploaders.</para>
/// <para><b>Failure semantics.</b> Each queued command's result is its own
/// <c>Task</c>. If any one fails, the batch as a whole still completes —
/// inspect the individual tasks (collected for you) to discover per-command
/// errors. There is no implicit rollback (Redis has no transaction
/// equivalent for cross-key writes; use MULTI / EXEC explicitly if you
/// need atomicity).</para>
/// <para><b>When to use.</b> Any batch ≥ ~10 items. Below that, per-call
/// awaits are simpler with negligible perf cost.</para>
/// </remarks>
public sealed class RedisBulkUploader
{
    private readonly RedisConnection _connection;
    private readonly ILogger<RedisBulkUploader> _logger;

    /// <summary>Initializes a new instance of <see cref="RedisBulkUploader"/>.</summary>
    /// <param name="connection">Long-lived connection wrapper. The uploader does not dispose it.</param>
    /// <param name="logger">Optional logger; defaults to <see cref="NullLogger{T}.Instance"/>.</param>
    public RedisBulkUploader(RedisConnection connection, ILogger<RedisBulkUploader>? logger = null)
    {
        ArgumentNullException.ThrowIfNull(connection);
        _connection = connection;
        _logger = logger ?? NullLogger<RedisBulkUploader>.Instance;
    }

    /// <summary>
    /// Pipelines a per-item lambda against a single <see cref="IBatch"/>:
    /// queues all the commands locally, fires them in one
    /// <see cref="IBatch.Execute"/>, awaits the collected per-command
    /// tasks. Returns the number of items processed (NOT the number of
    /// individual Redis commands issued — caller decides how many commands
    /// each item entails).
    /// </summary>
    /// <typeparam name="T">Item type. Each item passes through <paramref name="queueCommands"/> once.</typeparam>
    /// <param name="items">Items to process. Streamed — not buffered (other than the per-item Task list).</param>
    /// <param name="queueCommands">
    /// Per-item callback receiving the shared <see cref="IBatch"/>, the
    /// item, and a collector for the pending <see cref="Task"/>s. The
    /// callback should call <c>batch.HashSetAsync(...)</c> / equivalent
    /// (which queues the command locally) and add the returned Task to the
    /// collector. The uploader awaits all of them after firing the batch.
    /// </param>
    /// <param name="cancellationToken">Honoured during queuing and while awaiting completion.</param>
    /// <returns>Number of items processed (one per <paramref name="queueCommands"/> invocation).</returns>
    public async Task<long> BulkApplyAsync<T>(
        IEnumerable<T> items,
        Action<IBatch, T, ICollection<Task>> queueCommands,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(items);
        ArgumentNullException.ThrowIfNull(queueCommands);

        var batch = _connection.Database.CreateBatch();
        var pending = new List<Task>();
        long itemCount = 0;

        foreach (var item in items)
        {
            cancellationToken.ThrowIfCancellationRequested();
            queueCommands(batch, item, pending);
            itemCount++;
        }

        _logger.LogDebug("RedisBulkUpload firing batch: items={Items}, queued_commands={Commands}", itemCount, pending.Count);
        batch.Execute();
        await Task.WhenAll(pending).WaitAsync(cancellationToken);
        _logger.LogDebug("RedisBulkUpload complete: {Items} items processed", itemCount);
        return itemCount;
    }
}
