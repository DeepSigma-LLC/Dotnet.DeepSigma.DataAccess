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

### `RelationalDatabaseAPI`

A small Dapper wrapper that takes an `IDbConnectionFactory` and exposes async helpers for the common CRUD shapes. The factory is injected at construction so the same class works against any relational provider — SQL Server, Postgres, or anything else that implements `IDbConnectionFactory`.

| Method | Returns | Description |
|---|---|---|
| `GetAllAsync<T>(sql, [timeout])` | `IEnumerable<T>` | Query rows without parameters. |
| `GetAllAsync<Parameters, T>(sql, parameters, [timeout])` | `IEnumerable<T>` | Query rows with a parameter object. |
| `GetByIdAsync<T>(sql, id, [timeout])` | `T?` | Convenience wrapper that binds `{ Id = id }` and returns the first row. |
| `InsertAsync<Parameters>(sql, parameters, [timeout])` | `int` | Execute an `INSERT` that returns the generated id via `ExecuteScalar`. |
| `InsertAllAsync<F>(sql, parameters, [timeout])` | `IEnumerable<int>?` | Execute an `INSERT` against multiple parameter sets. |
| `UpdateAsync<Parameters>(sql, parameters, [timeout])` | `int` | Execute an `UPDATE` and return the affected scalar. |
| `UpdateAllAsync<F>(sql, parameters, [timeout])` | `IEnumerable<int>?` | Execute an `UPDATE` against multiple parameter sets. |
| `ExecuteAsync<Parameters, T>(sql, parameters, [timeout])` | `T?` | Execute arbitrary SQL and return a single scalar of type `T`. |

All connections are created via the factory and disposed at the end of each call. No connection pooling is layered on top — that is handled by the underlying provider (Microsoft.Data.SqlClient and Npgsql both pool connections by default).

## Quick start

```csharp
using DeepSigma.DataAccess.Abstraction;
using DeepSigma.DataAccess.Postgres;          // or DeepSigma.DataAccess.SqlServer
using DeepSigma.DataAccess.RelationalDatabase;

IDbConnectionFactory factory = new PostgresConnectionFactory(
    "Host=localhost;Database=appdb;Username=postgres;Password=postgres");

var db = new RelationalDatabaseAPI(factory);

// Query without parameters
IEnumerable<dynamic> activeUsers = await db.GetAllAsync<dynamic>(
    "SELECT id, name FROM users WHERE active = TRUE");

// Query with parameters
IEnumerable<UserDto> users = await db.GetAllAsync<{ int MinId }, UserDto>(
    "SELECT id, name FROM users WHERE id >= @MinId",
    new { MinId = 100 });

// Insert returning the new id
int newId = await db.InsertAsync(
    "INSERT INTO users (name) VALUES (@Name) RETURNING id",
    new { Name = "Ada" });
```

## Swapping providers

Because `RelationalDatabaseAPI` takes an `IDbConnectionFactory`, switching providers is a one-line change at composition time:

```csharp
// Test: use SQLite in-memory via your own factory
IDbConnectionFactory factory = new MyTestSqliteFactory();

// Production: Postgres
IDbConnectionFactory factory = new PostgresConnectionFactory(connectionString);
```

The rest of your data-access code stays identical.

## Notes

- All methods open a fresh connection per call. If you have a hot path that issues many queries, consider batching or running them inside a single Dapper call rather than chaining `RelationalDatabaseAPI` invocations.
- SQL dialect is **not** abstracted. You are still writing raw SQL; just make sure your SQL is valid for the provider you've injected.

## License

MIT
