# DeepSigma.DataAccess.Abstraction

Shared contracts and models for the `DeepSigma.DataAccess` family of provider packages. This package has **no external dependencies** — it only defines interfaces and POCOs that providers (SQL Server, Postgres, …) implement, and that consumers code against.

If you are building an application, you typically do **not** install this package directly. You install a provider package (e.g. `DeepSigma.DataAccess.SqlServer`) and Abstraction comes along transitively.

## Installation

```bash
dotnet add package DeepSigma.DataAccess.Abstraction
```

## Dependencies

None. This package is BCL-only.

## What it provides

### Interfaces

| Type | Purpose |
|---|---|
| `IDbConnectionFactory` | Creates `IDbConnection` instances. Provider packages supply the concrete factory (e.g. `SqlServerConnectionFactory`, `PostgresConnectionFactory`). |
| `IDatabaseSchemaService` | Returns schema metadata: tables, fields, constraints, and foreign keys. Each provider supplies its own implementation backed by provider-specific catalog queries. |

### Models

All models live under `DeepSigma.DataAccess.Abstraction.Models`.

| Type | Description |
|---|---|
| `TableName` | A table's schema + name. |
| `TableField` | A column: name, data type, length, precision, nullability, default flag. |
| `TableConstraint` | A non-foreign-key constraint: name, table schema, table name, column. |
| `TableForeignKey` | A foreign key: constraint name, foreign column, referenced (primary) table schema/name/column. |

## Why this package exists

Provider packages need to expose the same contracts so consumers can write provider-agnostic code:

```csharp
IDatabaseSchemaService schema = useSqlServer
    ? new SqlServerSchemaService(connectionString)
    : new PostgresSchemaService(connectionString);

IEnumerable<TableName> tables = await schema.GetTables();
```

Putting the contracts in a dependency-free package means the providers (and their consumers) can share types without dragging in Dapper, SqlClient, Npgsql, or any other runtime concern.

## Implementing a custom provider

If you want to add a new relational provider, you only need to implement the two interfaces:

```csharp
using System.Data;
using DeepSigma.DataAccess.Abstraction;

public sealed class MyDbConnectionFactory : IDbConnectionFactory
{
    private readonly string _connectionString;
    public MyDbConnectionFactory(string connectionString) => _connectionString = connectionString;
    public IDbConnection Create() => new MyDbConnection(_connectionString);
}
```

```csharp
using DeepSigma.DataAccess.Abstraction;
using DeepSigma.DataAccess.Abstraction.Models;

public sealed class MyDbSchemaService : IDatabaseSchemaService
{
    public Task<IEnumerable<TableName>>       GetTables()        => /* ... */;
    public Task<IEnumerable<TableField>>      GetTableFields()   => /* ... */;
    public Task<IEnumerable<TableConstraint>> GetConstraints()   => /* ... */;
    public Task<IEnumerable<TableForeignKey>> GetForeignKeys()   => /* ... */;
}
```

You can then plug your factory into `RelationalDatabaseAPI` from `DeepSigma.DataAccess.RelationalDatabase`.

## Versioning

This package is foundational — most other packages in the family depend on it. Breaking changes to these interfaces or models cascade across every provider, so they are made conservatively.

## License

MIT
