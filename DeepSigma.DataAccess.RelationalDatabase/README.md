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
| `GetByIdAsync<T>(sql, id, [timeout])` | `T?` | Convenience wrapper that binds `{ Id = id }` and returns the first row. `id` is `object`, so any Dapper-bindable type works. |
| `InsertAsync<TParam>(sql, parameters, [timeout])` | `int` | Execute an `INSERT` that returns the generated id via `ExecuteScalar`. SQL must include a `RETURNING id` (Postgres) or `OUTPUT INSERTED.Id` (SQL Server) clause. |
| `InsertAllAsync<TParam>(sql, parameters, [timeout])` | `int` | Execute the SQL once per parameter set; returns the total number of rows affected. Does **not** return generated ids — loop with `InsertAsync` if you need them. |
| `UpdateAsync<TParam>(sql, parameters, [timeout])` | `int` | Execute an `UPDATE` and return the number of affected rows. |
| `UpdateAllAsync<TParam>(sql, parameters, [timeout])` | `int` | Execute the SQL once per parameter set; returns the total number of rows affected across all sets. |
| `ExecuteAsync<TParam, T>(sql, parameters, [timeout])` | `T?` | Execute arbitrary SQL and return a single scalar of type `T`. |
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

// Insert returning the new id
int newId = await db.InsertAsync(
    "INSERT INTO users (name) VALUES (@Name) RETURNING id",
    new { Name = "Ada" });
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

All scope methods accept the same `commandTimeout` and `CancellationToken` parameters as the top-level `RelationalDatabaseApi` methods.

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
