# DeepSigma.DataAccess.Postgres.Pgvector

Opt-in [`pgvector`](https://github.com/pgvector/pgvector) wiring for
`DeepSigma.DataAccess.Postgres`. Pull this package only when you need vector
columns / similarity search — base Postgres consumers don't take the
`Pgvector` and `Pgvector.Dapper` transitive weight.

## Three entry points

### `PgvectorPostgresConnectionFactory.Create(...)` — one-line convenience

```csharp
using DeepSigma.DataAccess.Postgres.Pgvector;

var factory = PgvectorPostgresConnectionFactory.Create(connectionString);
// factory is a PostgresConnectionFactory backed by a pgvector-enabled NpgsqlDataSource,
// and the Pgvector.Dapper type handler is registered (once per AppDomain).
```

### `NpgsqlDataSourceBuilder.UsePgvector()` — for users building their own data source

```csharp
using DeepSigma.DataAccess.Postgres.Pgvector;
using Npgsql;

var dataSource = new NpgsqlDataSourceBuilder(connStr)
    .UsePgvector()
    // ... your other configuration (logging, type handlers, etc.) ...
    .Build();

var factory = new PostgresConnectionFactory(dataSource, ownsDataSource: true);
```

### `PgvectorDapper.RegisterTypeHandler()` — Dapper handler registration on its own

```csharp
PgvectorDapper.RegisterTypeHandler();   // safe to call multiple times; runs at most once per AppDomain
```
