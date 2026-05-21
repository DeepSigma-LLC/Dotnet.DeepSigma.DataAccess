# DeepSigma.DataAccess.SqlServer

SQL Server provider for the `DeepSigma.DataAccess` family. Supplies a `SqlConnection`-backed connection factory, a schema service that introspects tables / fields / constraints / foreign keys via `INFORMATION_SCHEMA`, and a high-throughput bulk-copy helper backed by `SqlBulkCopy`.

## Installation

```bash
dotnet add package DeepSigma.DataAccess.SqlServer
```

This transitively pulls in `DeepSigma.DataAccess.Abstraction`, `DeepSigma.DataAccess.RelationalDatabase`, `Dapper`, and `Microsoft.Data.SqlClient`.

## Dependencies

| Package | Purpose |
|---|---|
| `DeepSigma.DataAccess.Abstraction` | `IDbConnectionFactory`, `IDatabaseSchemaService`, `Table*` models. |
| `DeepSigma.DataAccess.RelationalDatabase` | `RelationalDatabaseApi` (Dapper-based CRUD). |
| `Microsoft.Data.SqlClient` | SQL Server ADO.NET driver. |

## What it provides

### `SqlServerConnectionFactory`

Implements `IDbConnectionFactory` by returning new `SqlConnection` instances bound to the supplied connection string. Connections are returned closed; Dapper will open them as needed.

```csharp
IDbConnectionFactory factory = new SqlServerConnectionFactory(
    "Server=localhost;Database=AppDb;Integrated Security=True;TrustServerCertificate=True;");
```

An optional `onConnectionOpened` callback is invoked every time a connection transitions to `Open`. Use it for per-connection `SET` statements:

```csharp
var factory = new SqlServerConnectionFactory(connectionString, onConnectionOpened: conn =>
{
    using var cmd = conn.CreateCommand();
    cmd.CommandText = "SET ANSI_NULLS ON; SET ARITHABORT ON; SET LOCK_TIMEOUT 5000;";
    cmd.ExecuteNonQuery();
});
```

### `SqlServerSchemaService`

Implements `IDatabaseSchemaService` by executing packaged SQL files against `INFORMATION_SCHEMA`. Constructor overloads accept either a connection string (convenience) or a pre-built `IDbConnectionFactory` (more flexible).

| Method | Returns | Source query |
|---|---|---|
| `GetTablesAsync()` | `IEnumerable<TableName>` | `INFORMATION_SCHEMA.TABLES` filtered to `BASE TABLE` in schema `dbo`. |
| `GetTableFieldsAsync()` | `IEnumerable<TableField>` | `INFORMATION_SCHEMA.COLUMNS` for schema `dbo`. |
| `GetConstraintsAsync()` | `IEnumerable<TableConstraint>` | `INFORMATION_SCHEMA.TABLE_CONSTRAINTS` joined to `KEY_COLUMN_USAGE`, excluding foreign keys. |
| `GetForeignKeysAsync()` | `IEnumerable<TableForeignKey>` | `INFORMATION_SCHEMA.REFERENTIAL_CONSTRAINTS` joined to `KEY_COLUMN_USAGE` and `TABLE_CONSTRAINTS`. |

The four `.sql` files live under `SQL/` in the package and are copied to the consumer's output directory at build time, then read at runtime via `AppDomain.CurrentDomain.BaseDirectory`. This makes the queries easy to inspect or replace without recompiling.

### `SqlServerBulkCopier`

A separate, deliberately-not-portable helper for high-throughput ingestion. Wraps `Microsoft.Data.SqlClient.SqlBulkCopy`. See the **[Bulk copy](#bulk-copy-high-throughput-ingestion)** section below for the full rationale and example — this is not a drop-in replacement for `InsertAllAsync`.

| Method | Returns | Description |
|---|---|---|
| `BulkCopyAsync<T>(destinationTable, rows, [batchSize], [bulkCopyTimeoutSeconds], [options], [ct])` | `long` (rows copied) | Streams POCO rows into the destination table via the TDS bulk-load protocol. Property names must match destination column names. |

## Quick start: schema discovery

```csharp
using DeepSigma.DataAccess.Abstraction.Models;
using DeepSigma.DataAccess.SqlServer;

var schema = new SqlServerSchemaService(
    "Server=localhost;Database=AppDb;Integrated Security=True;TrustServerCertificate=True;");

IEnumerable<TableName>       tables       = await schema.GetTablesAsync();
IEnumerable<TableField>      fields       = await schema.GetTableFieldsAsync();
IEnumerable<TableConstraint> constraints  = await schema.GetConstraintsAsync();
IEnumerable<TableForeignKey> foreignKeys  = await schema.GetForeignKeysAsync();
```

## Dependency-injection registration

```csharp
using Microsoft.Extensions.DependencyInjection;

services.AddDeepSigmaSqlServer(builder.Configuration.GetConnectionString("Default")!);
```

This registers (as singletons):

- `IDbConnectionFactory` → `SqlServerConnectionFactory`
- `RelationalDatabaseApi`
- `IDatabaseSchemaService` → `SqlServerSchemaService`
- `SqlServerBulkCopier`
- `MigrationRunner` — pre-wired with the SQL Server-flavoured `_migrations` DDL (see [Migrations](#migrations) below)

Consume them via constructor injection:

```csharp
public class TableInspector(IDatabaseSchemaService schema, RelationalDatabaseApi db)
{
    public async Task<IEnumerable<TableName>> ListTables(CancellationToken ct) => await schema.GetTablesAsync(ct);
}
```

If you also register the Postgres extension in the same container, both will compete for the `IDbConnectionFactory` and `IDatabaseSchemaService` registrations — register only one provider per service collection, or use keyed services (`AddKeyedSingleton(...)`).

## Quick start: ad-hoc queries

The factory plugs directly into `RelationalDatabaseApi` for general-purpose Dapper queries:

```csharp
using DeepSigma.DataAccess.Abstraction;
using DeepSigma.DataAccess.RelationalDatabase;
using DeepSigma.DataAccess.SqlServer;

IDbConnectionFactory factory = new SqlServerConnectionFactory(connectionString);
var db = new RelationalDatabaseApi(factory);

IEnumerable<UserDto> users = await db.GetAllAsync<UserDto>(
    "SELECT Id, Name FROM Users WHERE IsActive = 1");
```

## Bulk copy (high-throughput ingestion)

`SqlServerBulkCopier` deliberately sits **outside** the standard CRUD surface of `RelationalDatabaseApi`. It uses a different SQL Server wire protocol (TDS bulk load) with different semantics, and exposing it through the portable `InsertAllAsync` API would misrepresent what is happening.

### Why it exists

`RelationalDatabaseApi.InsertAllAsync` sends one `INSERT` per row. For ingestion-shaped workloads (ETL, nightly loads, migrations, telemetry backfills) that is **10–100× slower** than `SqlBulkCopy`, which:

- streams rows in TDS bulk format with no per-row parse / plan cost;
- batches index updates;
- can be configured for minimal transaction logging;
- skips triggers and constraint checks by default (configurable).

Use it when you are loading more than a few thousand rows in a single operation.

### Why it deviates from `InsertAllAsync`

| Concern | `InsertAllAsync` | `BulkCopyAsync` |
|---|---|---|
| Per-row error reporting | One row's failure rolls back that row's INSERT | Constraint violation rolls back the entire current batch |
| Generated identity values | Available via `RETURNING` / `OUTPUT` clauses | **Not returned** — copy into a staging table and `INSERT ... SELECT ... OUTPUT INSERTED.Id` if needed |
| Triggers | Always fire | Skipped by default (pass `SqlBulkCopyOptions.FireTriggers` to enable) |
| Constraint checks | Always run | Skipped by default (pass `SqlBulkCopyOptions.CheckConstraints` to enable) |
| Throughput | ~1× | ~10–100× |

### Quick start

```csharp
using DeepSigma.DataAccess.SqlServer;
using Microsoft.Data.SqlClient;

public sealed class TelemetryEvent
{
    public Guid EventId { get; init; }
    public DateTime Timestamp { get; init; }
    public string Source { get; init; } = "";
    public decimal Value { get; init; }
}

var bulkCopier = new SqlServerBulkCopier(connectionString);

// Stream from any deferred IEnumerable — rows are NOT buffered in memory.
IEnumerable<TelemetryEvent> events = ReadEventsFromFile("daily.csv");

long copied = await bulkCopier.BulkCopyAsync(
    destinationTable: "dbo.TelemetryEvents",
    rows: events,
    batchSize: 10_000,
    cancellationToken: ct);

Console.WriteLine($"Loaded {copied} rows.");
```

### POCO → column mapping

- Every public readable property on `T` is mapped to a destination column **of the same name**.
- No `[Column]` attribute support — use a DTO that mirrors the destination table if you need a different shape.
- Property types must be compatible with the destination column type (the usual ADO.NET conversions apply).

### Memory

Rows are streamed via an internal `ObjectDataReader<T>` adapter — the full sequence is **not** materialized in memory before sending. Pass any deferred `IEnumerable<T>` (file-reader, paged query, on-demand generator) freely.

### When *not* to use

If your workload is "user submits a form, we insert 1–5 rows", stick with `RelationalDatabaseApi.InsertAsync` / `InsertAllAsync`. You get per-row results, generated IDs, and trigger firing for free, with imperceptible latency at small row counts.

## Migrations

`AddDeepSigmaSqlServer` auto-registers a `MigrationRunner` pre-wired with the SQL Server DDL for the `_migrations` tracking table:

```sql
IF NOT EXISTS (SELECT 1 FROM sys.objects WHERE type = 'U' AND name = '_migrations')
BEGIN
    CREATE TABLE _migrations (
        Id NVARCHAR(255) NOT NULL PRIMARY KEY,
        AppliedAtUtc DATETIME2 NOT NULL
    );
END
```

Resolve it from DI and hand it an ordered list of `Migration` records:

```csharp
using DeepSigma.DataAccess.RelationalDatabase;

public class Schema(MigrationRunner runner)
{
    private static readonly Migration[] All =
    [
        new("20260101_001", "CREATE TABLE Users (Id INT IDENTITY(1,1) PRIMARY KEY, Name NVARCHAR(255) NOT NULL);"),
        new("20260108_002", "ALTER TABLE Users ADD Email NVARCHAR(320) NULL;"),
        new("20260115_003", "CREATE INDEX IX_Users_Email ON Users (Email);"),
    ];

    public Task<IReadOnlyList<string>> EnsureLatestAsync(CancellationToken ct) => runner.ApplyAsync(All, ct);
}
```

Each migration runs in its own transaction along with the bookkeeping `INSERT` into `_migrations`, so a failure rolls back cleanly with no partial state. Re-running with the same list is a no-op — already-applied ids are skipped silently.

SQL Server's `GO` batch separator is **not** supported (it's a `sqlcmd` directive, not T-SQL). Split multi-statement migrations into separate `Migration` records — that also gives you fine-grained tracking and the ability to add a description per change.

See the [RelationalDatabase README](../DeepSigma.DataAccess.RelationalDatabase/README.md#migrations) for design rationale and the full method contract.

## Health checks

Pairs with the `Microsoft.Extensions.Diagnostics.HealthChecks` system. The check opens a connection and runs `SELECT 1`.

```csharp
services.AddHealthChecks()
    .AddDeepSigmaSqlServer(connectionString, tags: new[] { "readiness" });

app.MapHealthChecks("/health");
```

Optional arguments (all named): `name` (default `"deepsigma_sqlserver"`), `failureStatus` (default `Unhealthy` — pass `HealthStatus.Degraded` to keep readiness green during transient outages), `tags`, `timeout` (recommended: `TimeSpan.FromSeconds(5)`).

## Notes

- The packaged SQL queries default to schema `dbo`. If your tables live in a different schema, copy the SQL files out of the package and adjust the `WHERE TABLE_SCHEMA = 'dbo'` clauses to suit.
- `GetTableFieldsAsync()` includes `TableName` in the projection — group by `(TableSchema, TableName)` to reconstruct per-table column lists.
- `GetConstraintsAsync()` uses a `LEFT JOIN` to `KEY_COLUMN_USAGE`, so `CHECK` constraints (which have no column-usage rows) are included with a null `ColumnName`. Filter by `ConstraintType` if you want only PK / UNIQUE rows.
- Connections honour whatever pooling/timeout/encryption settings you set in the connection string.

## License

MIT
