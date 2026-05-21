namespace DeepSigma.DataAccess.RelationalDatabase;

/// <summary>
/// A single, idempotently-applied database change.
/// </summary>
/// <param name="Id">
/// Stable, unique identifier for the migration — typically a timestamped string such as
/// <c>"20260101_001_create_users"</c>. Once a migration has been applied, its id is recorded in the
/// <c>_migrations</c> table; future runs that see the same id will skip it.
/// </param>
/// <param name="Sql">
/// The SQL to execute. May contain multiple statements separated by semicolons if the underlying
/// provider supports multi-statement batches (SQLite, Postgres, SQL Server all do).
/// </param>
/// <param name="Description">Optional human-readable description, written to the log when the migration runs.</param>
public sealed record Migration(string Id, string Sql, string? Description = null);
