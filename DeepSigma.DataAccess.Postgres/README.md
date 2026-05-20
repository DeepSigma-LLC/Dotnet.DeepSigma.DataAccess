# DeepSigma.DataAccess.Postgres

PostgreSQL provider for the `DeepSigma.DataAccess` family. Supplies an `NpgsqlConnection`-backed connection factory, a schema service that introspects tables / fields / constraints / foreign keys via `information_schema`, and a high-throughput bulk-copy helper backed by Postgres's binary `COPY` protocol.

## Installation

```bash
dotnet add package DeepSigma.DataAccess.Postgres
```

This transitively pulls in `DeepSigma.DataAccess.Abstraction`, `DeepSigma.DataAccess.RelationalDatabase`, `Dapper`, and `Npgsql`.

## Dependencies

| Package | Purpose |
|---|---|
| `DeepSigma.DataAccess.Abstraction` | `IDbConnectionFactory`, `IDatabaseSchemaService`, `Table*` models. |
| `DeepSigma.DataAccess.RelationalDatabase` | `RelationalDatabaseApi` (Dapper-based CRUD). |
| `Npgsql` | PostgreSQL ADO.NET driver. |

## What it provides

### `PostgresConnectionFactory`

Implements `IDbConnectionFactory` by returning new `NpgsqlConnection` instances bound to the supplied connection string.

```csharp
IDbConnectionFactory factory = new PostgresConnectionFactory(
    "Host=localhost;Database=appdb;Username=postgres;Password=postgres");
```

### `PostgresSchemaService`

Implements `IDatabaseSchemaService` by executing packaged SQL files against `information_schema`. Constructor overloads accept either a connection string or a pre-built `IDbConnectionFactory`.

| Method | Returns | Source query |
|---|---|---|
| `GetTablesAsync()` | `IEnumerable<TableName>` | `information_schema.tables` filtered to `BASE TABLE` in schema `public`. |
| `GetTableFieldsAsync()` | `IEnumerable<TableField>` | `information_schema.columns` for schema `public`. |
| `GetConstraintsAsync()` | `IEnumerable<TableConstraint>` | `information_schema.table_constraints` joined to `key_column_usage`, excluding foreign keys. |
| `GetForeignKeysAsync()` | `IEnumerable<TableForeignKey>` | `information_schema.referential_constraints` joined to `key_column_usage` and `table_constraints`. |

The four `.sql` files live under `SQL/` in the package and are copied to the consumer's output directory at build time, then read at runtime via `AppDomain.CurrentDomain.BaseDirectory`. Inspect or override them as needed.

### `PostgresBulkCopier`

A separate, deliberately-not-portable helper for high-throughput ingestion. Wraps `Npgsql.NpgsqlBinaryImporter` (`COPY … FROM STDIN BINARY`). See the **[Bulk copy](#bulk-copy-high-throughput-ingestion)** section below for the full rationale and example — this is not a drop-in replacement for `InsertAllAsync`.

| Method | Returns | Description |
|---|---|---|
| `BulkCopyAsync<T>(destinationTable, rows, [ct])` | `long` (rows copied) | Streams POCO rows into the destination table via the Postgres binary `COPY` protocol. Property names must match destination column names. |

## Quick start: schema discovery

```csharp
using DeepSigma.DataAccess.Abstraction.Models;
using DeepSigma.DataAccess.Postgres;

var schema = new PostgresSchemaService(
    "Host=localhost;Database=appdb;Username=postgres;Password=postgres");

IEnumerable<TableName>       tables       = await schema.GetTablesAsync();
IEnumerable<TableField>      fields       = await schema.GetTableFieldsAsync();
IEnumerable<TableConstraint> constraints  = await schema.GetConstraintsAsync();
IEnumerable<TableForeignKey> foreignKeys  = await schema.GetForeignKeysAsync();
```

## Dependency-injection registration

```csharp
using Microsoft.Extensions.DependencyInjection;

services.AddDeepSigmaPostgres(builder.Configuration.GetConnectionString("Default")!);
```

This registers (as singletons):

- `IDbConnectionFactory` → `PostgresConnectionFactory`
- `RelationalDatabaseApi`
- `IDatabaseSchemaService` → `PostgresSchemaService`
- `PostgresBulkCopier`

Consume them via constructor injection:

```csharp
public class TableInspector(IDatabaseSchemaService schema, RelationalDatabaseApi db)
{
    public async Task<IEnumerable<TableName>> ListTables(CancellationToken ct) => await schema.GetTablesAsync(ct);
}
```

If you also register the SQL Server extension in the same container, both will compete for the `IDbConnectionFactory` and `IDatabaseSchemaService` registrations — register only one provider per service collection, or use keyed services (`AddKeyedSingleton(...)`).

## Quick start: ad-hoc queries

```csharp
using DeepSigma.DataAccess.Abstraction;
using DeepSigma.DataAccess.Postgres;
using DeepSigma.DataAccess.RelationalDatabase;

IDbConnectionFactory factory = new PostgresConnectionFactory(connectionString);
var db = new RelationalDatabaseApi(factory);

IEnumerable<UserDto> users = await db.GetAllAsync<UserDto>(
    "SELECT id, name FROM users WHERE active = TRUE");
```

## Bulk copy (high-throughput ingestion)

`PostgresBulkCopier` deliberately sits **outside** the standard CRUD surface of `RelationalDatabaseApi`. It uses Postgres's `COPY … FROM STDIN BINARY` protocol, which has different semantics from row-by-row `INSERT`. Exposing it through the portable `InsertAllAsync` API would misrepresent what is happening.

### Why it exists

`RelationalDatabaseApi.InsertAllAsync` sends one `INSERT` per row. For ingestion-shaped workloads (ETL, nightly loads, migrations, telemetry backfills) that is **10–100× slower** than `COPY`, which:

- streams rows in Postgres's binary format with no per-row parse / plan cost;
- writes to the WAL with much less per-row overhead;
- batches index updates;
- skips trigger evaluation for the COPY itself (row-level triggers don't fire).

Use it when you are loading more than a few thousand rows in a single operation.

### Why it deviates from `InsertAllAsync`

| Concern | `InsertAllAsync` | `BulkCopyAsync` |
|---|---|---|
| Per-row error reporting | One row's failure rolls back that row's INSERT | Constraint violation aborts the entire `COPY` — no partial-success state |
| Generated identity values | Available via `RETURNING` clauses | **Not returned** — copy into a staging table and `INSERT ... SELECT ... RETURNING id` if needed |
| Row-level triggers | Fire as normal | Do not fire (statement-level triggers do fire once) |
| Throughput | ~1× | ~10–100× |

### Quick start

```csharp
using DeepSigma.DataAccess.Postgres;

public sealed class TelemetryEvent
{
    public Guid EventId { get; init; }
    public DateTime Timestamp { get; init; }
    public string Source { get; init; } = "";
    public decimal Value { get; init; }
}

var bulkCopier = new PostgresBulkCopier(connectionString);

// Stream from any deferred IEnumerable — rows are NOT buffered in memory.
IEnumerable<TelemetryEvent> events = ReadEventsFromFile("daily.csv");

long copied = await bulkCopier.BulkCopyAsync(
    destinationTable: "public.telemetry_events",
    rows: events,
    cancellationToken: ct);

Console.WriteLine($"Loaded {copied} rows.");
```

### POCO → column mapping

- Every public readable property on `T` is mapped to a destination column **of the same name**.
- Column names are emitted **quoted** in the `COPY` command (e.g. `"EventId"`). If your Postgres table uses lowercase / snake_case column names (the Postgres convention), define your POCO with properties named to match (`event_id`, `timestamp`, …), or wrap them in a DTO that mirrors the table.
- No `[Column]` attribute support.

### Type handling

Each value is passed to `NpgsqlBinaryImporter.WriteAsync(value)`, which infers the Postgres type from the CLR type. This works for most BCL types — numerics, strings, dates, GUIDs, byte arrays, enums. For unusual types (e.g. PostGIS geometries, custom composite types) you may need to switch to row-by-row `INSERT`.

### Memory

Rows are streamed via Npgsql's binary importer — the full sequence is **not** materialized in memory before sending. Pass any deferred `IEnumerable<T>` (file-reader, paged query, on-demand generator) freely.

### When *not* to use

If your workload is "user submits a form, we insert 1–5 rows", stick with `RelationalDatabaseApi.InsertAsync` / `InsertAllAsync`. You get per-row results, generated IDs, and trigger firing for free, with imperceptible latency at small row counts.

## Health checks

Pairs with the `Microsoft.Extensions.Diagnostics.HealthChecks` system. The check opens a connection and runs `SELECT 1`.

```csharp
services.AddHealthChecks()
    .AddDeepSigmaPostgres(connectionString, tags: new[] { "readiness" });

app.MapHealthChecks("/health");
```

Optional arguments (all named): `name` (default `"deepsigma_postgres"`), `failureStatus` (default `Unhealthy` — pass `HealthStatus.Degraded` to keep readiness green during transient outages), `tags`, `timeout` (recommended: `TimeSpan.FromSeconds(5)`).

## Notes

- The packaged queries target schema `public`. To inspect a different schema, copy the SQL files out of the package and adjust the `WHERE table_schema = 'public'` clauses.
- `GetTableFieldsAsync()` includes `TableName` in the projection — group by `(TableSchema, TableName)` to reconstruct per-table column lists.
- `GetConstraintsAsync()` uses a `LEFT JOIN` to `key_column_usage`, so `CHECK` constraints (which have no column-usage rows) are included with a null `ColumnName`. Filter by `ConstraintType` if you want only PK / UNIQUE rows.
- Postgres folds unquoted identifiers to lowercase, so the packaged SQL uses quoted aliases (e.g. `AS "TableSchema"`) to match the PascalCase property names on the `Table*` models. Keep this in mind if you customise the SQL.
- Npgsql pools connections by default — let your connection string control the pool size and lifetime.

## License

MIT
