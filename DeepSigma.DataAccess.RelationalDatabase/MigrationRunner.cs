using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace DeepSigma.DataAccess.RelationalDatabase;

/// <summary>
/// Applies an ordered set of <see cref="Migration"/>s against a relational database, recording each
/// applied id in a <c>_migrations</c> tracking table so subsequent runs skip already-applied changes.
/// </summary>
/// <remarks>
/// <para>
/// The runner is provider-agnostic — the only provider-specific piece is the DDL that creates the
/// tracking table (the syntax for "create table if not exists" differs slightly per engine). Provider
/// DI extensions supply the correct DDL; you generally do not construct this class by hand.
/// </para>
/// <para>
/// Each migration runs inside its own transaction along with the bookkeeping <c>INSERT</c> into
/// <c>_migrations</c>, so a failing migration leaves no partial state behind. Migrations are processed
/// in the order they are enumerated — supply them in a deterministic order (sorted list, fixed array).
/// </para>
/// </remarks>
public sealed class MigrationRunner
{
    private readonly RelationalDatabaseApi _db;
    private readonly string _createMigrationsTableSql;
    private readonly ILogger<MigrationRunner> _logger;

    /// <summary>Initializes a new instance of <see cref="MigrationRunner"/>.</summary>
    /// <param name="db">The Dapper-backed database API.</param>
    /// <param name="createMigrationsTableSql">
    /// Provider-specific DDL that creates the <c>_migrations</c> table if it does not already exist.
    /// The table must have at minimum an <c>Id</c> column (string PK) and an <c>AppliedAtUtc</c> column
    /// (timestamp).
    /// </param>
    /// <param name="logger">Optional logger. Defaults to <see cref="NullLogger{T}.Instance"/>.</param>
    public MigrationRunner(
        RelationalDatabaseApi db,
        string createMigrationsTableSql,
        ILogger<MigrationRunner>? logger = null)
    {
        _db = db;
        _createMigrationsTableSql = createMigrationsTableSql;
        _logger = logger ?? NullLogger<MigrationRunner>.Instance;
    }

    /// <summary>
    /// Ensures the <c>_migrations</c> tracking table exists, then applies any migrations whose id has not
    /// already been recorded. Idempotent — re-running with the same migration list is a no-op.
    /// </summary>
    /// <param name="migrations">
    /// The migrations to apply, in the desired order. Already-applied ids are skipped silently
    /// (logged at Debug level).
    /// </param>
    /// <param name="cancellationToken">Cancellation token honoured between migrations and during each one.</param>
    /// <returns>The ids of migrations that were newly applied during this call, in the order they ran.</returns>
    public async Task<IReadOnlyList<string>> ApplyAsync(
        IEnumerable<Migration> migrations,
        CancellationToken cancellationToken = default)
    {
        await _db.ExecuteAsync(_createMigrationsTableSql, cancellationToken: cancellationToken);

        // Column references below (Id, AppliedAtUtc) are intentionally unquoted. Postgres folds
        // unquoted identifiers to lowercase, SqlServer / SQLite match identifiers case-insensitively,
        // and each provider's _migrations DDL creates the columns unquoted too — so a single SQL
        // string works against all three engines.
        IEnumerable<string> appliedIds = await _db.GetAllAsync<string>(
            "SELECT Id FROM _migrations",
            cancellationToken: cancellationToken);
        var applied = new HashSet<string>(appliedIds, StringComparer.Ordinal);

        var newlyApplied = new List<string>();
        foreach (Migration migration in migrations)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (applied.Contains(migration.Id))
            {
                _logger.LogDebug("Migration {Id} already applied; skipping", migration.Id);
                continue;
            }

            _logger.LogInformation(
                "Applying migration {Id}{Description}",
                migration.Id,
                migration.Description is null ? string.Empty : $": {migration.Description}");

            await using RelationalDatabaseTransactionScope tx =
                await _db.BeginTransactionAsync(cancellationToken: cancellationToken);
            await tx.ExecuteAsync(migration.Sql, cancellationToken: cancellationToken);
            await tx.ExecuteAsync(
                "INSERT INTO _migrations (Id, AppliedAtUtc) VALUES (@Id, @AppliedAtUtc)",
                new { Id = migration.Id, AppliedAtUtc = DateTime.UtcNow },
                cancellationToken: cancellationToken);
            await tx.CommitAsync(cancellationToken);

            newlyApplied.Add(migration.Id);
        }
        return newlyApplied;
    }
}
