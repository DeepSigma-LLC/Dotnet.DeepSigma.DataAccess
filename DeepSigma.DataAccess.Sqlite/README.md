# DeepSigma.DataAccess.Sqlite

SQLite provider for the `DeepSigma.DataAccess` family. Supplies a `SqliteConnection`-backed connection factory plus a schema service that introspects tables, fields, constraints, and foreign keys via SQLite's `sqlite_master` table and PRAGMA functions.

Particularly useful for:
- **Unit testing** relational code against an in-memory database (`Data Source=:memory:`) with no external infrastructure.
- **Embedded applications** that ship a local file-based database.

## Installation

```bash
dotnet add package DeepSigma.DataAccess.Sqlite
```

This transitively pulls in `DeepSigma.DataAccess.Abstraction`, `DeepSigma.DataAccess.RelationalDatabase`, `Dapper`, and `Microsoft.Data.Sqlite`.

## Dependencies

| Package | Purpose |
|---|---|
| `DeepSigma.DataAccess.Abstraction` | `IDbConnectionFactory`, `IDatabaseSchemaService`, `Table*` models. |
| `DeepSigma.DataAccess.RelationalDatabase` | `RelationalDatabaseApi` (Dapper-based CRUD). |
| `Microsoft.Data.Sqlite` | SQLite ADO.NET driver. |

## What it provides

### `SqliteConnectionFactory`

Implements `IDbConnectionFactory` by returning new `SqliteConnection` instances bound to the supplied connection string. Common connection strings:

| String | Meaning |
|---|---|
| `Data Source=app.db` | File-based database in the current working directory. |
| `Data Source=:memory:` | Private in-memory database — disappears when the connection closes. **Per-connection** — separate `IDbConnection`s see separate databases. |
| `Data Source=file:test?mode=memory&cache=shared` | Shared in-memory database — multiple connections see the same data. Use this when you want in-memory speed but more than one connection (e.g. integration tests where the schema is set up in one connection and queried in another). |

```csharp
IDbConnectionFactory factory = new SqliteConnectionFactory("Data Source=app.db");
```

### `SqliteSchemaService`

Implements `IDatabaseSchemaService` by executing packaged SQL files against `sqlite_master` plus the PRAGMA table-valued functions (`pragma_table_info`, `pragma_index_list`, `pragma_index_info`, `pragma_foreign_key_list`).

| Method | Returns | Source query |
|---|---|---|
| `GetTablesAsync()` | `IEnumerable<TableName>` | `sqlite_master` filtered to `type = 'table'`, excluding `sqlite_%` system tables. |
| `GetTableFieldsAsync()` | `IEnumerable<TableField>` | `sqlite_master` joined to `pragma_table_info(name)` for each table. |
| `GetConstraintsAsync()` | `IEnumerable<TableConstraint>` | Primary keys (from `pragma_table_info.pk`) plus user-declared `UNIQUE` constraints (from `pragma_index_list` where `origin = 'u'`). |
| `GetForeignKeysAsync()` | `IEnumerable<TableForeignKey>` | `sqlite_master` joined to `pragma_foreign_key_list(name)`. |

## Dependency-injection registration

```csharp
using Microsoft.Extensions.DependencyInjection;

services.AddDeepSigmaSqlite("Data Source=app.db");
```

This registers (as singletons):

- `IDbConnectionFactory` → `SqliteConnectionFactory`
- `RelationalDatabaseApi`
- `IDatabaseSchemaService` → `SqliteSchemaService`

If you also register the SQL Server or Postgres extension in the same container, both will compete for the `IDbConnectionFactory` and `IDatabaseSchemaService` registrations — register only one provider per service collection, or use keyed services (`AddKeyedSingleton(...)`).

## Quick start: schema discovery

```csharp
using DeepSigma.DataAccess.Abstraction.Models;
using DeepSigma.DataAccess.Sqlite;

var schema = new SqliteSchemaService("Data Source=app.db");

IEnumerable<TableName>       tables       = await schema.GetTablesAsync();
IEnumerable<TableField>      fields       = await schema.GetTableFieldsAsync();
IEnumerable<TableConstraint> constraints  = await schema.GetConstraintsAsync();
IEnumerable<TableForeignKey> foreignKeys  = await schema.GetForeignKeysAsync();
```

## Quick start: ad-hoc queries

```csharp
using DeepSigma.DataAccess.Abstraction;
using DeepSigma.DataAccess.RelationalDatabase;
using DeepSigma.DataAccess.Sqlite;

IDbConnectionFactory factory = new SqliteConnectionFactory("Data Source=app.db");
var db = new RelationalDatabaseApi(factory);

IEnumerable<UserDto> users = await db.GetAllAsync<UserDto>(
    "SELECT id, name FROM users WHERE active = 1");
```

## In-memory for unit tests

The shared in-memory pattern is the most useful one for tests:

```csharp
const string connectionString = "Data Source=file:test_db_for_my_test?mode=memory&cache=shared";

// First connection sets up schema:
var factory = new SqliteConnectionFactory(connectionString);
var db = new RelationalDatabaseApi(factory);
await db.ExecuteAsync<object, int>("CREATE TABLE users (id INTEGER PRIMARY KEY, name TEXT)", null);
await db.InsertAsync("INSERT INTO users (name) VALUES (@Name)", new { Name = "Ada" });

// Subsequent connections (via the same factory) see the same data.
var found = await db.GetAllAsync<UserDto>("SELECT id, name FROM users");
```

Use a unique name (`test_db_for_my_test`) per test class to keep tests isolated.

## Why there is no `SqliteBulkCopier`

The SQL Server and Postgres providers expose dedicated `SqlServerBulkCopier` / `PostgresBulkCopier` classes because each database has a **native bulk-load protocol** distinct from `INSERT`. SQLite has no such protocol — there is no `COPY` or `BULK INSERT` equivalent. The fast path for large ingestion in SQLite is simply **transactions plus a prepared `INSERT`**, which the standard CRUD API already supports:

```csharp
await using var tx = await db.BeginTransactionAsync(cancellationToken: ct);
await tx.InsertAllAsync(
    "INSERT INTO users (name, email) VALUES (@Name, @Email)",
    users,
    cancellationToken: ct);
await tx.CommitAsync(ct);
```

Without the surrounding transaction, SQLite calls `fsync` after every row to flush WAL — single-row inserts top out at a few hundred per second on a typical SSD. With a transaction, the fsync only happens at commit, and you can easily hit tens of thousands of inserts per second.

For maximum throughput, also consider:
- `PRAGMA journal_mode = WAL;` (run once per database; persists in the file)
- `PRAGMA synchronous = NORMAL;` (per-connection; trades a tiny durability window for substantial speed)
- `PRAGMA temp_store = MEMORY;` (per-connection)

Set these via a quick `ExecuteAsync` after opening the database.

## Health checks

Pairs with the `Microsoft.Extensions.Diagnostics.HealthChecks` system. The check opens a connection and runs `SELECT 1`.

```csharp
services.AddHealthChecks()
    .AddDeepSigmaSqlite("Data Source=app.db", tags: new[] { "readiness" });

app.MapHealthChecks("/health");
```

Optional arguments (all named): `name` (default `"deepsigma_sqlite"`), `failureStatus`, `tags`, `timeout`. For SQLite this is most useful as a "the file is reachable and the SQLite library answers" smoke test — it does **not** validate that any specific table or migration is present.

## Notes

- **SQLite is dynamically typed.** The `DataType` in `TableField` is a *hint* declared in `CREATE TABLE`, not an enforced constraint. Rows can store values of any type regardless of the declared type.
- **`CharacterMaximumLength` and `NumericPrecision` are always null.** SQLite has no equivalent concepts.
- **`GetConstraintsAsync()`** returns `PRIMARY KEY` rows (from each column flagged in `pragma_table_info`) and user-declared `UNIQUE` rows. `CHECK` constraints are not surfaced — SQLite stores them only in the original `CREATE TABLE` text, and there is no queryable list. Filter by `ConstraintType` if you need only one kind.
- **Foreign keys are anonymous** in SQLite, so `ConstraintName` is always null. Also, foreign-key enforcement requires `PRAGMA foreign_keys = ON` at the **connection** level — it does *not* persist in the database file. The schema service still reports declared FKs regardless of whether enforcement is on.
- **Connection strings.** `Data Source=:memory:` is per-connection — the most common cause of "my test data disappeared" is creating a fresh `SqliteConnection` and finding it empty. Use the `file:name?mode=memory&cache=shared` form when you need sharing.
- **Threading.** `SqliteConnection` is not thread-safe — the connection factory pattern handles this naturally because each call gets its own connection.

## License

MIT
