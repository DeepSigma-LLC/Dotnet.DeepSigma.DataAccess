# DeepSigma.DataAccess.Postgres

PostgreSQL provider for the `DeepSigma.DataAccess` family. Supplies an `NpgsqlConnection`-backed connection factory plus a schema service that introspects tables, fields, constraints, and foreign keys via `information_schema`.

## Installation

```bash
dotnet add package DeepSigma.DataAccess.Postgres
```

This transitively pulls in `DeepSigma.DataAccess.Abstraction`, `DeepSigma.DataAccess.RelationalDatabase`, `Dapper`, and `Npgsql`.

## Dependencies

| Package | Purpose |
|---|---|
| `DeepSigma.DataAccess.Abstraction` | `IDbConnectionFactory`, `IDatabaseSchemaService`, `Table*` models. |
| `DeepSigma.DataAccess.RelationalDatabase` | `RelationalDatabaseAPI` (Dapper-based CRUD). |
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
| `GetTables()` | `IEnumerable<TableName>` | `information_schema.tables` filtered to `BASE TABLE` in schema `public`. |
| `GetTableFields()` | `IEnumerable<TableField>` | `information_schema.columns` for schema `public`. |
| `GetConstraints()` | `IEnumerable<TableConstraint>` | `information_schema.table_constraints` joined to `key_column_usage`, excluding foreign keys. |
| `GetForeignKeys()` | `IEnumerable<TableForeignKey>` | `information_schema.referential_constraints` joined to `key_column_usage` and `table_constraints`. |

The four `.sql` files live under `SQL/` in the package and are copied to the consumer's output directory at build time, then read at runtime via `AppDomain.CurrentDomain.BaseDirectory`. Inspect or override them as needed.

## Quick start: schema discovery

```csharp
using DeepSigma.DataAccess.Abstraction.Models;
using DeepSigma.DataAccess.Postgres;

var schema = new PostgresSchemaService(
    "Host=localhost;Database=appdb;Username=postgres;Password=postgres");

IEnumerable<TableName>       tables       = await schema.GetTables();
IEnumerable<TableField>      fields       = await schema.GetTableFields();
IEnumerable<TableConstraint> constraints  = await schema.GetConstraints();
IEnumerable<TableForeignKey> foreignKeys  = await schema.GetForeignKeys();
```

## Quick start: ad-hoc queries

```csharp
using DeepSigma.DataAccess.Abstraction;
using DeepSigma.DataAccess.Postgres;
using DeepSigma.DataAccess.RelationalDatabase;

IDbConnectionFactory factory = new PostgresConnectionFactory(connectionString);
var db = new RelationalDatabaseAPI(factory);

IEnumerable<UserDto> users = await db.GetAllAsync<UserDto>(
    "SELECT id, name FROM users WHERE active = TRUE");
```

## Notes

- The packaged queries target schema `public`. To inspect a different schema, copy the SQL files out of the package and adjust the `WHERE table_schema = 'public'` clauses.
- Postgres folds unquoted identifiers to lowercase, so the packaged SQL uses quoted aliases (e.g. `AS "TableSchema"`) to match the PascalCase property names on the `Table*` models. Keep this in mind if you customise the SQL.
- Npgsql pools connections by default — let your connection string control the pool size and lifetime.

## License

MIT
