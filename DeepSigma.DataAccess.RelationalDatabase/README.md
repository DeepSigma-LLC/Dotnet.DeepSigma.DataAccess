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
