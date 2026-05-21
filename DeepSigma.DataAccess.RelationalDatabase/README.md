# DeepSigma.DataAccess.RelationalDatabase

Shared Dapper-backed CRUD class for relational databases. This package contains the provider-agnostic SQL execution layer; pair it with a provider package (`DeepSigma.DataAccess.SqlServer`, `DeepSigma.DataAccess.Postgres`, …) to supply the actual database connection.

## Installation

You typically don't install this directly — installing a provider package pulls it in transitively. If you want it on its own:

```bash
dotnet add package DeepSigma.DataAccess.RelationalDatabase
```

## Dependencies

| Package | Purpose |
|---|---|
| `DeepSigma.DataAccess.Abstraction` | Defines `IDbConnectionFactory`. |
| `Dapper` | Object-relational mapping for parameterized SQL. |

## What it provides

| Type | Purpose |
|---|---|
| `RelationalDatabaseApi` | Dapper-backed CRUD, streaming, scalar, and transaction helpers — the main thing you'll call. |
| `RelationalDatabaseTransactionScope` | Returned by `BeginTransactionAsync`; mirrors the CRUD surface and threads the transaction through every call. |
| `MigrationRunner` + `Migration` record | Idempotent schema migrations against the `_migrations` tracking table. Auto-registered by every provider's `AddDeepSigma*` extension. |
| `RelationalConnectionFactoryBase<TConnection>` | Public base for custom `IDbConnectionFactory` implementations — handles the per-connection-opened callback wiring. See [Building a custom provider](#building-a-custom-provider). |
| `RelationalSchemaServiceBase` | Public base for custom `IDatabaseSchemaService` implementations — handles the SQL-file lookup and Dapper invocation. |

### `RelationalDatabaseApi`

A small Dapper wrapper that takes an `IDbConnectionFactory` and exposes async helpers for the common CRUD shapes. The factory is injected at construction so the same class works against any relational provider — SQL Server, Postgres, or anything else that implements `IDbConnectionFactory`.

| Method | Returns | Description |
|---|---|---|
| `GetAllAsync<T>(sql, [timeout])` | `IEnumerable<T>` | Query rows without parameters. |
| `GetAllAsync<TParam, T>(sql, parameters, [timeout])` | `IEnumerable<T>` | Query rows with a parameter object. |
| `GetByIdAsync<T>(sql, id, [timeout])` | `T?` | Convenience wrapper that binds `{ Id = id }` and returns the first row. Equivalent to `QueryFirstOrDefaultAsync` with a pre-bound `@Id` parameter. |
| `QueryFirstOrDefaultAsync<T>(sql, [timeout])` | `T?` | First row matching `sql`, or `default` when none. Tolerates extras — only the first is returned. |
| `QueryFirstOrDefaultAsync<TParam, T>(sql, parameters, [timeout])` | `T?` | Same with a parameter object. |
| `QuerySingleOrDefaultAsync<T>(sql, [timeout])` | `T?` | Single row matching `sql`, or `default` when none. **Throws `InvalidOperationException` if more than one row** — use for uniqueness checks. |
| `QuerySingleOrDefaultAsync<TParam, T>(sql, parameters, [timeout])` | `T?` | Same with a parameter object. |
| `InsertAsync<TParam>(sql, parameters, [timeout])` | `int` | Execute an `INSERT` that returns the generated id via `ExecuteScalar`. SQL must include a `RETURNING id` (Postgres) or `OUTPUT INSERTED.Id` (SQL Server) clause. |
| `InsertAllAsync<TParam>(sql, parameters, [timeout])` | `int` | Execute the SQL once per parameter set; returns the total number of rows affected. Does **not** return generated ids — loop with `InsertAsync` if you need them. |
| `UpdateAsync(sql, [timeout])` | `int` | Execute a non-query SQL statement (parameterless UPDATE / DELETE / DDL). Returns affected row count (may be `-1` for DDL on some providers). |
| `UpdateAsync<TParam>(sql, parameters, [timeout])` | `int` | Same with a parameter object. |
| `UpdateAllAsync<TParam>(sql, parameters, [timeout])` | `int` | Execute the SQL once per parameter set; returns the total number of rows affected across all sets. |
| `ExecuteAsync(sql, [timeout])` | `int` | **Preferred** general-purpose non-query executor (Dapper convention). Identical behaviour to `UpdateAsync` but reads naturally for `DELETE`, `INSERT … ON CONFLICT`, DDL, etc. |
| `ExecuteAsync<TParam>(sql, parameters, [timeout])` | `int` | Same with a parameter object. |
| `ExecuteAllAsync<TParam>(sql, parameters, [timeout])` | `int` | Same as `UpdateAllAsync` — preferred name for non-UPDATE batches. |
| `ExecuteScalarAsync<T>(sql, [timeout])` | `T?` | Execute SQL and return the first column of the first row as `T`. Typical use: `SELECT COUNT(*)`, `SELECT MAX(...)`, single-value lookups. |
| `ExecuteScalarAsync<TParam, T>(sql, parameters, [timeout])` | `T?` | Same with a parameter object. |
| `QueryStreamAsync<T>(sql, [timeout])` | `IAsyncEnumerable<T>` | Stream rows lazily via `Dapper.QueryUnbufferedAsync` — connection stays open until enumeration ends. |
| `QueryStreamAsync<TParam, T>(sql, parameters, [timeout])` | `IAsyncEnumerable<T>` | Same with a parameter object. |
| `BeginTransactionAsync([isolationLevel])` | `RelationalDatabaseTransactionScope` | Opens a connection, begins a transaction, and returns a scope mirroring the CRUD methods. See "Transactions" below. |

All methods accept a trailing `CancellationToken cancellationToken = default` parameter. All connections are created via the factory and disposed at the end of each call. No connection pooling is layered on top — that is handled by the underlying provider (Microsoft.Data.SqlClient and Npgsql both pool connections by default).

### Logging

Optional `ILogger<RelationalDatabaseApi>` is accepted by the constructor (defaults to `NullLogger`). When registered via DI, ASP.NET automatically resolves the logger. Each method emits a `Debug`-level entry on invocation.

## Quick start

```csharp
using DeepSigma.DataAccess.Abstraction;
using DeepSigma.DataAccess.Postgres;          // or DeepSigma.DataAccess.SqlServer
using DeepSigma.DataAccess.RelationalDatabase;

IDbConnectionFactory factory = new PostgresConnectionFactory(
    "Host=localhost;Database=appdb;Username=postgres;Password=postgres");

var db = new RelationalDatabaseApi(factory);

// Query without parameters
IEnumerable<dynamic> activeUsers = await db.GetAllAsync<dynamic>(
    "SELECT id, name FROM users WHERE active = TRUE");

// Query with parameters (anonymous object — TParam is inferred)
IEnumerable<UserDto> users = await db.GetAllAsync<object, UserDto>(
    "SELECT id, name FROM users WHERE id >= @MinId",
    new { MinId = 100 });

// Get one row by an arbitrary WHERE clause — replaces the awkward
// `(await GetAllAsync<...>(...)).FirstOrDefault()` pattern.
UserDto? user = await db.QueryFirstOrDefaultAsync<object, UserDto>(
    "SELECT id, name FROM users WHERE email = @Email",
    new { Email = "ada@example.com" });

// Uniqueness check — throws if a duplicate email slipped past the constraint.
UserDto? unique = await db.QuerySingleOrDefaultAsync<object, UserDto>(
    "SELECT id, name FROM users WHERE email = @Email",
    new { Email = "ada@example.com" });

// Insert returning the new id
int newId = await db.InsertAsync(
    "INSERT INTO users (name) VALUES (@Name) RETURNING id",
    new { Name = "Ada" });

// Scalar — single value lookup
long? count = await db.ExecuteScalarAsync<long?>(
    "SELECT COUNT(*) FROM users WHERE active = TRUE");

// DDL — no parameters, fire-and-forget
await db.UpdateAsync("CREATE TABLE IF NOT EXISTS audit (id SERIAL PRIMARY KEY, message TEXT)");
```

## Streaming large result sets

Use `QueryStreamAsync<T>` when the result set is too large to materialize in memory. It is backed by `Dapper.QueryUnbufferedAsync` and yields rows as they arrive:

```csharp
await foreach (UserDto user in db.QueryStreamAsync<UserDto>(
    "SELECT id, name FROM users", cancellationToken: ct))
{
    await ProcessAsync(user, ct);   // back-pressure-friendly
}
```

The underlying connection stays open until the enumerator is disposed (either by the `await foreach` completing or via early disposal). Don't capture and hold the `IAsyncEnumerable<T>` beyond the lifetime of your call site.

## Stored procedures

Every method that accepts SQL also accepts an optional `CommandType? commandType = null` parameter. Pass `CommandType.StoredProcedure` to have Dapper treat the SQL string as a procedure name and bind parameters by name to the procedure's declared parameters — instead of executing the string as a free-form SQL statement.

```csharp
// Input parameters only — pass the procedure name as the SQL string,
// parameters as an anonymous object, and CommandType.StoredProcedure.
IEnumerable<UserDto> users = await db.GetAllAsync<object, UserDto>(
    "GetActiveUsers",
    new { MinId = 100 },
    commandType: CommandType.StoredProcedure);

// Single result via QueryFirstOrDefaultAsync
UserDto? user = await db.QueryFirstOrDefaultAsync<object, UserDto>(
    "GetUserByEmail",
    new { Email = "ada@example.com" },
    commandType: CommandType.StoredProcedure);

// A non-query stored procedure (no result set)
await db.UpdateAsync(
    "ArchiveOldOrders",
    new { OlderThan = DateTime.UtcNow.AddYears(-5) },
    commandType: CommandType.StoredProcedure);
```

You can still execute stored procedures with `commandType: null` (the default) by writing the SQL as `"EXEC GetActiveUsers @MinId"`. The two approaches behave equivalently for the common "input parameters + one result set" shape; `CommandType.StoredProcedure` is cleaner for that case and is required for procedures that use **`OUTPUT` / `RETURN` parameters** — wire those up via Dapper's `DynamicParameters`:

```csharp
var parameters = new DynamicParameters();
parameters.Add("@MinId", 100, DbType.Int32);
parameters.Add("@Total", dbType: DbType.Int32, direction: ParameterDirection.Output);

IEnumerable<UserDto> users = await db.GetAllAsync<DynamicParameters, UserDto>(
    "GetUsersAboveWithTotal",
    parameters,
    commandType: CommandType.StoredProcedure);

int total = parameters.Get<int>("@Total");
```

## Transactions

`BeginTransactionAsync` opens a connection, starts a transaction, and returns a `RelationalDatabaseTransactionScope` that mirrors the CRUD methods. The scope is `IAsyncDisposable` — disposing without calling `CommitAsync()` rolls back.

```csharp
await using RelationalDatabaseTransactionScope tx = await db.BeginTransactionAsync(cancellationToken: ct);

int newUserId = await tx.InsertAsync(
    "INSERT INTO users (name) VALUES (@Name) RETURNING id",
    new { Name = "Ada" }, cancellationToken: ct);

await tx.InsertAsync(
    "INSERT INTO audit (user_id, event) VALUES (@UserId, @Event)",
    new { UserId = newUserId, Event = "created" }, cancellationToken: ct);

await tx.CommitAsync(ct);   // omit this and the scope rolls back on dispose
```

You can pass an `IsolationLevel` if the default isn't what you want:

```csharp
await using var tx = await db.BeginTransactionAsync(IsolationLevel.Serializable, ct);
```

The scope mirrors the full method surface of `RelationalDatabaseApi` — `GetAllAsync`, `GetByIdAsync`, `QueryFirstOrDefaultAsync`, `QuerySingleOrDefaultAsync`, `InsertAsync` / `InsertAllAsync`, `UpdateAsync` / `UpdateAllAsync`, `ExecuteScalarAsync`, plus `CommitAsync`. All scope methods accept the same `commandTimeout`, `commandType`, and `CancellationToken` parameters. There is no streaming method inside a transaction scope — `QueryStreamAsync` only exists on the top-level API.

## Migrations

`MigrationRunner` applies an ordered list of `Migration`s once each, tracking applied ids in a `_migrations` table so subsequent runs are no-ops. It is **deliberately tiny** — no naming conventions, no file scanning, no "down" scripts, no embedded-resource discovery. You supply an ordered `IEnumerable<Migration>` from wherever makes sense for your project (a static array, a directory of `.sql` files you read at startup, etc.) and the runner makes them idempotent.

```csharp
public sealed record Migration(string Id, string Sql, string? Description = null);
```

```csharp
// Resolved from DI (registered automatically by every provider's AddDeepSigma* extension)
public class StartupMigrations(MigrationRunner runner)
{
    private static readonly Migration[] All =
    [
        new("20260101_001", "CREATE TABLE users (id SERIAL PRIMARY KEY, name TEXT NOT NULL);"),
        new("20260108_002", "ALTER TABLE users ADD COLUMN email TEXT;", "back-fill nullable"),
        new("20260115_003", "CREATE INDEX users_email_idx ON users (email);"),
    ];

    public Task ApplyAsync(CancellationToken ct) => runner.ApplyAsync(All, ct);
}
```

### Behaviour

- Creates the `_migrations` tracking table on first call (provider-specific DDL is supplied by the registering DI extension). The DDL uses `CREATE TABLE IF NOT EXISTS` / equivalent so the call is safe to repeat.
- Reads the set of already-applied ids in a single query.
- For each migration whose id is **not** in that set, opens a transaction, runs the migration SQL, inserts the id into `_migrations`, and commits. A failing migration rolls back both the SQL and the tracking row, leaving no partial state.
- Returns the list of newly-applied ids (in run order).
- Already-applied ids are skipped silently (Debug-level log entry).

### Tracking table schema

| Column | Type (varies by provider) | Purpose |
|---|---|---|
| `Id` | `TEXT` / `NVARCHAR(255)` — primary key | The migration id you supplied. |
| `AppliedAtUtc` | `TEXT` / `TIMESTAMPTZ` / `DATETIME2` | UTC timestamp when the migration ran. |

You can read this table directly if you need to introspect what's been applied:

```csharp
IEnumerable<(string Id, DateTime AppliedAtUtc)> history = await db.GetAllAsync<(string, DateTime)>(
    "SELECT Id, AppliedAtUtc FROM _migrations ORDER BY AppliedAtUtc");
```

### Design notes

- **No "down" migrations.** Reversible migrations are nice in theory and a footgun in practice. If you need to back out a change, write a forward migration that does so.
- **No file-system / assembly scanning.** Discovery is the consumer's job — it's a `for` loop. Centralising it would force opinions about layout, naming, and ordering that aren't this library's call.
- **Run order = enumeration order.** Sort your list (or use a sorted-on-disk filename convention) before passing it in.
- **Provider DDL is supplied at registration.** The runner itself only knows portable SQL. Each provider's `AddDeepSigma*` DI extension wires in the right `CREATE TABLE IF NOT EXISTS` flavour.

## Building a custom provider

The shipped providers (`SqlServer`, `Postgres`, `Sqlite`) cover the common cases, but if you need MySQL, Oracle, DuckDB, or another ADO.NET-compatible engine, this package provides two public base classes you can extend. The shipped providers themselves are built on top of these, so the path is identical.

### `RelationalConnectionFactoryBase<TConnection>`

Handles the per-connection-opened callback wiring (the `StateChange` event), so subclasses only declare the connection type and how to construct one:

```csharp
using DeepSigma.DataAccess.RelationalDatabase;
using MySqlConnector;          // hypothetical MySQL driver

public sealed class MySqlConnectionFactory : RelationalConnectionFactoryBase<MySqlConnection>
{
    private readonly string _connectionString;

    public MySqlConnectionFactory(string connectionString, Action<MySqlConnection>? onConnectionOpened = null)
        : base(onConnectionOpened)
    {
        _connectionString = connectionString;
    }

    protected override MySqlConnection CreateConnectionCore() => new(_connectionString);
}
```

That's the whole factory. The base class implements `IDbConnectionFactory.Create()` and invokes `onConnectionOpened` every time a connection transitions to `Open`. The generic constraint is `TConnection : DbConnection`, which all ADO.NET providers satisfy.

### `RelationalSchemaServiceBase`

Wires the four `IDatabaseSchemaService` methods to packaged `.sql` files. Subclasses only supply a file-name prefix; the base resolves `{AppDomain.BaseDirectory}/SQL/{prefix}_TableNames.sql` (and the three siblings) and runs them through `RelationalDatabaseApi`.

```csharp
using DeepSigma.DataAccess.Abstraction;
using DeepSigma.DataAccess.RelationalDatabase;
using Microsoft.Extensions.Logging;

public sealed class MySqlSchemaService : RelationalSchemaServiceBase
{
    public MySqlSchemaService(string connectionString, ILogger<MySqlSchemaService>? logger = null)
        : this(new MySqlConnectionFactory(connectionString), logger) { }

    public MySqlSchemaService(IDbConnectionFactory factory, ILogger<MySqlSchemaService>? logger = null)
        : base(factory, filePrefix: "MySql", logger) { }
}
```

Add four `.sql` files to your project (`MySql_TableNames.sql`, `MySql_TableAndFieldInfo.sql`, `MySql_Constraints.sql`, `MySql_ForeignKeyConstraints.sql`) returning rows shaped like the `Table*` models in `DeepSigma.DataAccess.Abstraction`. Set `<CopyToOutputDirectory>PreserveNewest</CopyToOutputDirectory>` on each so they end up in `{output}/SQL/`. The packaged `.sql` files in the three shipped providers are a good starting reference.

### Wire it into DI

A custom `AddDeepSigmaMySql` extension follows the same shape as the shipped ones, including auto-registering `MigrationRunner` with a MySQL-flavoured DDL:

```csharp
public static class DeepSigmaMySqlServiceCollectionExtensions
{
    internal const string CreateMigrationsTableSql =
        "CREATE TABLE IF NOT EXISTS _migrations (Id VARCHAR(255) NOT NULL PRIMARY KEY, AppliedAtUtc DATETIME(6) NOT NULL);";

    public static IServiceCollection AddDeepSigmaMySql(
        this IServiceCollection services,
        string connectionString,
        Action<MySqlConnection>? onConnectionOpened = null)
    {
        services.AddSingleton<IDbConnectionFactory>(_ => new MySqlConnectionFactory(connectionString, onConnectionOpened));
        services.AddSingleton<RelationalDatabaseApi>();
        services.AddSingleton<IDatabaseSchemaService, MySqlSchemaService>();
        services.AddSingleton(sp => ActivatorUtilities.CreateInstance<MigrationRunner>(sp, CreateMigrationsTableSql));
        return services;
    }
}
```

That's it — your provider now plugs into `RelationalDatabaseApi`, `MigrationRunner`, transactions, health-check patterns, and any consumer code that takes `IDbConnectionFactory` / `IDatabaseSchemaService` / `RelationalDatabaseApi`.

## Swapping providers

Because `RelationalDatabaseApi` takes an `IDbConnectionFactory`, switching providers is a one-line change at composition time:

```csharp
// Test: use SQLite in-memory via your own factory
IDbConnectionFactory factory = new MyTestSqliteFactory();

// Production: Postgres
IDbConnectionFactory factory = new PostgresConnectionFactory(connectionString);
```

The rest of your data-access code stays identical.

## Notes

- All methods open a fresh connection per call. If you have a hot path that issues many queries, consider batching or running them inside a single Dapper call rather than chaining `RelationalDatabaseApi` invocations.
- SQL dialect is **not** abstracted. You are still writing raw SQL; just make sure your SQL is valid for the provider you've injected.

## License

MIT
