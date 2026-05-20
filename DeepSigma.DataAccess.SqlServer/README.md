# DeepSigma.DataAccess.SqlServer

SQL Server provider for the `DeepSigma.DataAccess` family. Supplies a `SqlConnection`-backed connection factory plus a schema service that introspects tables, fields, constraints, and foreign keys via `INFORMATION_SCHEMA`.

## Installation

```bash
dotnet add package DeepSigma.DataAccess.SqlServer
```

This transitively pulls in `DeepSigma.DataAccess.Abstraction`, `DeepSigma.DataAccess.RelationalDatabase`, `Dapper`, and `Microsoft.Data.SqlClient`.

## Dependencies

| Package | Purpose |
|---|---|
| `DeepSigma.DataAccess.Abstraction` | `IDbConnectionFactory`, `IDatabaseSchemaService`, `Table*` models. |
| `DeepSigma.DataAccess.RelationalDatabase` | `RelationalDatabaseAPI` (Dapper-based CRUD). |
| `Microsoft.Data.SqlClient` | SQL Server ADO.NET driver. |

## What it provides

### `SqlServerConnectionFactory`

Implements `IDbConnectionFactory` by returning new `SqlConnection` instances bound to the supplied connection string. Connections are returned closed; Dapper will open them as needed.

```csharp
IDbConnectionFactory factory = new SqlServerConnectionFactory(
    "Server=localhost;Database=AppDb;Integrated Security=True;TrustServerCertificate=True;");
```

### `SqlServerSchemaService`

Implements `IDatabaseSchemaService` by executing packaged SQL files against `INFORMATION_SCHEMA`. Constructor overloads accept either a connection string (convenience) or a pre-built `IDbConnectionFactory` (more flexible).

| Method | Returns | Source query |
|---|---|---|
| `GetTables()` | `IEnumerable<TableName>` | `INFORMATION_SCHEMA.TABLES` filtered to `BASE TABLE` in schema `dbo`. |
| `GetTableFields()` | `IEnumerable<TableField>` | `INFORMATION_SCHEMA.COLUMNS` for schema `dbo`. |
| `GetConstraints()` | `IEnumerable<TableConstraint>` | `INFORMATION_SCHEMA.TABLE_CONSTRAINTS` joined to `KEY_COLUMN_USAGE`, excluding foreign keys. |
| `GetForeignKeys()` | `IEnumerable<TableForeignKey>` | `INFORMATION_SCHEMA.REFERENTIAL_CONSTRAINTS` joined to `KEY_COLUMN_USAGE` and `TABLE_CONSTRAINTS`. |

The four `.sql` files live under `SQL/` in the package and are copied to the consumer's output directory at build time, then read at runtime via `AppDomain.CurrentDomain.BaseDirectory`. This makes the queries easy to inspect or replace without recompiling.

## Quick start: schema discovery

```csharp
using DeepSigma.DataAccess.Abstraction.Models;
using DeepSigma.DataAccess.SqlServer;

var schema = new SqlServerSchemaService(
    "Server=localhost;Database=AppDb;Integrated Security=True;TrustServerCertificate=True;");

IEnumerable<TableName>       tables       = await schema.GetTables();
IEnumerable<TableField>      fields       = await schema.GetTableFields();
IEnumerable<TableConstraint> constraints  = await schema.GetConstraints();
IEnumerable<TableForeignKey> foreignKeys  = await schema.GetForeignKeys();
```

## Quick start: ad-hoc queries

The factory plugs directly into `RelationalDatabaseAPI` for general-purpose Dapper queries:

```csharp
using DeepSigma.DataAccess.Abstraction;
using DeepSigma.DataAccess.RelationalDatabase;
using DeepSigma.DataAccess.SqlServer;

IDbConnectionFactory factory = new SqlServerConnectionFactory(connectionString);
var db = new RelationalDatabaseAPI(factory);

IEnumerable<UserDto> users = await db.GetAllAsync<UserDto>(
    "SELECT Id, Name FROM Users WHERE IsActive = 1");
```

## Notes

- The packaged SQL queries default to schema `dbo`. If your tables live in a different schema, copy the SQL files out of the package and adjust the `WHERE TABLE_SCHEMA = 'dbo'` clauses to suit.
- `GetTableFields()` does **not** include the table name in the projection (the `TableField` model has `TableSchema` and `ColumnName` but not `TableName`). If you need per-table grouping, join the result with `GetTables()` on `TableSchema`, or modify the SQL.
- Connections honour whatever pooling/timeout/encryption settings you set in the connection string.

## License

MIT
