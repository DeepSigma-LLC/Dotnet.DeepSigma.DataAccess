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

Implements `IDbConnectionFactory` by returning `NpgsqlConnection` instances drawn from a long-lived `NpgsqlDataSource` — the modern Npgsql 7+ idiom. The data source owns connection pooling, type-mapping configuration, password rotation, and per-source logging.

```csharp
IDbConnectionFactory factory = new PostgresConnectionFactory(
    "Host=localhost;Database=appdb;Username=postgres;Password=postgres");
```

Two construction patterns are available:

| Constructor | Ownership | Use when |
|---|---|---|
| `new PostgresConnectionFactory(connectionString, [onOpened])` | Factory builds and **owns** the `NpgsqlDataSource`. Disposing the factory disposes the data source. | The common case — you just want a working factory. |
| `new PostgresConnectionFactory(dataSource, [onOpened])` | Caller retains ownership. | You built the data source yourself (with `NpgsqlDataSourceBuilder` for custom type handlers, enum/composite mapping, password providers, etc.) and want full control over its lifetime. |
| `new PostgresConnectionFactory(dataSource, ownsDataSource: true, [onOpened])` | Explicit ownership flag. | Advanced: you built the data source inside a factory method that has no other owner (e.g. a DI lambda) and want the factory to dispose it. |

The factory implements `IDisposable`; when it owns the data source it forwards `Dispose()` to `NpgsqlDataSource.Dispose()`.

The optional `onConnectionOpened` callback is invoked every time a connection transitions to `Open`. Use it for per-connection `SET` statements:

```csharp
var factory = new PostgresConnectionFactory(connectionString, onConnectionOpened: conn =>
{
    using var cmd = conn.CreateCommand();
    cmd.CommandText = "SET search_path = analytics, public; SET statement_timeout = '30s';";
    cmd.ExecuteNonQuery();
});
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
- `MigrationRunner` — pre-wired with the Postgres-flavoured `_migrations` DDL (see [Migrations](#migrations) below)

Two optional callbacks customise the registration:

```csharp
services.AddDeepSigmaPostgres(
    connectionString,
    configureDataSource: builder =>
    {
        // Customise the NpgsqlDataSourceBuilder — type handlers, enum / composite mapping,
        // password providers, per-source logging, etc.
        builder.MapEnum<OrderStatus>("order_status");
        builder.EnableDynamicJson();
    },
    onConnectionOpened: conn =>
    {
        // Runs every time a connection transitions to Open — for per-connection SET statements.
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SET search_path = analytics, public;";
        cmd.ExecuteNonQuery();
    });
```

When `configureDataSource` is supplied, the DI lambda builds the `NpgsqlDataSource` and the factory takes ownership — disposal flows back through the DI container.

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

## Migrations

`AddDeepSigmaPostgres` auto-registers a `MigrationRunner` pre-wired with the Postgres DDL for the `_migrations` tracking table:

```sql
CREATE TABLE IF NOT EXISTS _migrations (Id TEXT NOT NULL PRIMARY KEY, AppliedAtUtc TIMESTAMPTZ NOT NULL);
```

Resolve it from DI and hand it an ordered list of `Migration` records:

```csharp
using DeepSigma.DataAccess.RelationalDatabase;

public class Schema(MigrationRunner runner)
{
    private static readonly Migration[] All =
    [
        new("20260101_001", "CREATE TABLE users (id SERIAL PRIMARY KEY, name TEXT NOT NULL);"),
        new("20260108_002", "ALTER TABLE users ADD COLUMN email TEXT;"),
        new("20260115_003", "CREATE INDEX users_email_idx ON users (email);"),
    ];

    public Task<IReadOnlyList<string>> EnsureLatestAsync(CancellationToken ct) => runner.ApplyAsync(All, ct);
}
```

Each migration runs in its own transaction along with the bookkeeping `INSERT` into `_migrations`, so a failure rolls back cleanly with no partial state. Re-running with the same list is a no-op — already-applied ids are skipped silently.

See the [RelationalDatabase README](../DeepSigma.DataAccess.RelationalDatabase/README.md#migrations) for design rationale and the full method contract.

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
