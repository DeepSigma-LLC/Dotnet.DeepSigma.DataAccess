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

All models are immutable `record`s with `init`-only properties.

| Type | Description |
|---|---|
| `TableName` | A table's schema + name. |
| `TableField` | A column: table schema/name, column name, data type, length, precision, nullability, and the default-value expression (`ColumnDefault: string?`). Convenience computed `HasDefault: bool`. |
| `TableConstraint` | A non-foreign-key constraint (primary key / unique / check): name, **constraint type**, table schema/name, column. |
| `TableForeignKey` | A foreign key: constraint name, foreign column, referenced (primary) table schema/name/column. |

## Why this package exists

Provider packages need to expose the same contracts so consumers can write provider-agnostic code:

```csharp
IDatabaseSchemaService schema = useSqlServer
    ? new SqlServerSchemaService(connectionString)
    : new PostgresSchemaService(connectionString);

IEnumerable<TableName> tables = await schema.GetTablesAsync();
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
    public Task<IEnumerable<TableName>>       GetTablesAsync()        => /* ... */;
    public Task<IEnumerable<TableField>>      GetTableFieldsAsync()   => /* ... */;
    public Task<IEnumerable<TableConstraint>> GetConstraintsAsync()   => /* ... */;
    public Task<IEnumerable<TableForeignKey>> GetForeignKeysAsync()   => /* ... */;
}
```

You can then plug your factory into `RelationalDatabaseApi` from `DeepSigma.DataAccess.RelationalDatabase`.

### Easier: inherit from the shipped base classes

If you are building a Dapper-backed relational provider (the common case), don't implement these interfaces from scratch — `DeepSigma.DataAccess.RelationalDatabase` ships `RelationalConnectionFactoryBase<TConnection>` and `RelationalSchemaServiceBase` that absorb the boilerplate (StateChange wiring, SQL-file lookup, Dapper invocation). Each concrete provider then becomes a 10–15 line shim. See [Building a custom provider](../DeepSigma.DataAccess.RelationalDatabase/README.md#building-a-custom-provider) in the RelationalDatabase README for a full MySQL walkthrough.

Implement the raw interfaces directly only when your provider is **not** Dapper- or ADO.NET-backed (e.g. a thin REST gateway, an in-memory mock, a different connection abstraction).

## Versioning

This package is foundational — most other packages in the family depend on it. Breaking changes to these interfaces or models cascade across every provider, so they are made conservatively.

## License

MIT
