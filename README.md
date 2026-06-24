# DeepSigma.DataAccess

A modular family of .NET data-access packages for relational databases, document stores, caches, blob storage, and HTTP APIs. Each storage technology is shipped as its own NuGet package so consumers only pull in the dependencies they actually use.

This repository contains 10 published packages plus shared abstractions. They target **.NET 10.0** and are MIT-licensed.

## Why modular packages?

The previous `DeepSigma.DataAccess` (1.x) was a single package that bundled Azure SDKs, MongoDB, SQL Server, Postgres, Redis, and HTTP utilities together. Consumers needing only Postgres ended up with Cosmos, Mongo, and Redis transitively.

The 2.x layout splits each storage concern into its own package, with a small shared `Abstraction` package holding interfaces and models. Provider packages depend only on what they need.

## Package matrix

| Package | Purpose | Key dependency |
|---|---|---|
| [DeepSigma.DataAccess.Abstraction](DeepSigma.DataAccess.Abstraction/README.md) | Shared interfaces (`IDbConnectionFactory`, `IDatabaseSchemaService`) and schema models (`TableName`, `TableField`, `TableConstraint`, `TableForeignKey`). | _(none)_ |
| [DeepSigma.DataAccess.RelationalDatabase](DeepSigma.DataAccess.RelationalDatabase/README.md) | Shared Dapper-backed `RelationalDatabaseApi` (CRUD + transactions + streaming), `MigrationRunner` (idempotent schema migrations), and the `RelationalConnectionFactoryBase<TConnection>` / `RelationalSchemaServiceBase` extension points for building a custom provider. Pair with a provider package. | Dapper |
| [DeepSigma.DataAccess.SqlServer](DeepSigma.DataAccess.SqlServer/README.md) | SQL Server connection factory, schema service, and `SqlBulkCopy`-based bulk-copier. | Microsoft.Data.SqlClient |
| [DeepSigma.DataAccess.Postgres](DeepSigma.DataAccess.Postgres/README.md) | PostgreSQL connection factory, schema service, and binary-`COPY`-based bulk-copier. | Npgsql |
| [DeepSigma.DataAccess.Sqlite](DeepSigma.DataAccess.Sqlite/README.md) | SQLite connection factory + schema service. Particularly useful for unit-testing relational code with no live infrastructure. | Microsoft.Data.Sqlite |
| [DeepSigma.DataAccess.MongoDB](DeepSigma.DataAccess.MongoDB/README.md) | MongoDB CRUD helpers built around an `IMongoDocument` contract. | MongoDB.Driver |
| [DeepSigma.DataAccess.Cosmos](DeepSigma.DataAccess.Cosmos/README.md) | Azure Cosmos DB helpers for databases, containers, items, and throughput. | Microsoft.Azure.Cosmos, DeepSigma.Core |
| [DeepSigma.DataAccess.Redis](DeepSigma.DataAccess.Redis/README.md) | Redis cache get/set/remove helpers. | StackExchange.Redis, Newtonsoft.Json |
| [DeepSigma.DataAccess.AzureBlobStorage](DeepSigma.DataAccess.AzureBlobStorage/README.md) | Azure Blob Storage upload, download, delete, and list. | Azure.Storage.Blobs |
| [DeepSigma.DataAccess.Http](DeepSigma.DataAccess.Http/README.md) | Helpers for JSON / CSV / XML API access — read (`GET`) and write (`POST`/`PUT`/`PATCH`/`DELETE`) verbs, streaming downloads, plus throttle/retry `DelegatingHandler`s. | DeepSigma.DataAccess.CsvUtilities |

## Dependency graph

```
                              Abstraction
                             /     |
                            /      |
            RelationalDatabase     |
            /     |     \          |
     SqlServer  Postgres  Sqlite   |
                                   |
   (independent leaves)            |
   MongoDB                         |
   Cosmos ─── DeepSigma.Core       |
   Redis                           |
   AzureBlobStorage                |
   Http ───── DeepSigma.DataAccess.CsvUtilities
```

- **Abstraction** has no dependencies.
- **RelationalDatabase** depends on Abstraction + Dapper.
- **SqlServer**, **Postgres**, and **Sqlite** depend on RelationalDatabase (and transitively Abstraction).
- **MongoDB**, **Cosmos**, **Redis**, **AzureBlobStorage**, and **Http** are independent of the relational stack — they each ship only their own driver dependency.

## Choosing what to install

Pick the packages that match the stores you use:

- "I just want to call Postgres with Dapper" → install **DeepSigma.DataAccess.Postgres** (pulls Abstraction + RelationalDatabase + Npgsql).
- "I just want SQL Server schema discovery" → install **DeepSigma.DataAccess.SqlServer**.
- "I just need Mongo CRUD" → install **DeepSigma.DataAccess.MongoDB**.
- "I want to unit-test my repository against an in-memory database" → install **DeepSigma.DataAccess.Sqlite** and use `Data Source=:memory:`.
- "Cosmos + Blob + Redis" → install those three packages independently; no relational dependencies will come along.

## Quick start: relational + provider

```bash
dotnet add package DeepSigma.DataAccess.Postgres
```

```csharp
using DeepSigma.DataAccess.Abstraction;
using DeepSigma.DataAccess.Postgres;
using DeepSigma.DataAccess.RelationalDatabase;

IDbConnectionFactory factory = new PostgresConnectionFactory(
    "Host=localhost;Database=appdb;Username=postgres;Password=postgres");

var db = new RelationalDatabaseApi(factory);

var users = await db.GetAllAsync<dynamic>("SELECT id, name FROM users WHERE active = TRUE");
```

To swap to SQL Server, change the factory and the connection string — `RelationalDatabaseApi` stays the same:

```csharp
IDbConnectionFactory factory = new SqlServerConnectionFactory(
    "Server=localhost;Database=AppDb;Integrated Security=True;TrustServerCertificate=True;");
```

## Quick start: dependency injection

Every provider package ships a `services.AddDeepSigmaX(...)` extension. The example above becomes:

```csharp
using Microsoft.Extensions.DependencyInjection;

services.AddDeepSigmaPostgres(builder.Configuration.GetConnectionString("Default")!);
// services.AddDeepSigmaSqlServer(connStr);
// services.AddDeepSigmaSqlite("Data Source=app.db");
// services.AddDeepSigmaMongoDb(connStr);
// services.AddDeepSigmaCosmos(endpointUri, apiKey, appName);
// services.AddDeepSigmaRedis(connStr, instanceName);
// services.AddDeepSigmaAzureBlobStorage(connStr, containerName);
// services.AddDeepSigmaHttp();  // registers HttpApi with IHttpClientFactory
```

Then consume via constructor injection:

```csharp
public class UserRepository(RelationalDatabaseApi db)
{
    public Task<IEnumerable<UserDto>> GetActive(CancellationToken ct)
        => db.GetAllAsync<UserDto>("SELECT id, name FROM users WHERE active = TRUE", cancellationToken: ct);
}
```

All registrations are **singletons** — the underlying connection / client objects (`CosmosClient`, `MongoClient`, `ConnectionMultiplexer`, `BlobContainerClient`) are designed to be shared. `CosmosDbApi` implements `IDisposable`, so the container disposes it on shutdown. See each provider's README for the exact registration shape.

## Migrations

Every relational provider (`SqlServer`, `Postgres`, `Sqlite`) auto-registers a `MigrationRunner` singleton when you call its `AddDeepSigma*` extension. Hand it an ordered list of `Migration(id, sql, description?)` records and it applies anything not yet recorded in the `_migrations` tracking table — each migration in its own transaction, so a failure leaves no partial state.

```csharp
public sealed record Migration(string Id, string Sql, string? Description = null);

public class StartupMigrations(MigrationRunner runner)
{
    private static readonly Migration[] All =
    [
        new("20260101_001", "CREATE TABLE users (id SERIAL PRIMARY KEY, name TEXT NOT NULL);"),
        new("20260108_002", "ALTER TABLE users ADD COLUMN email TEXT;"),
        new("20260115_003", "CREATE INDEX users_email_idx ON users (email);"),
    ];

    public Task<IReadOnlyList<string>> ApplyAsync(CancellationToken ct) => runner.ApplyAsync(All, ct);
}
```

The runner returns the ids that were newly applied this run. Re-running with the same list is a no-op. See the [RelationalDatabase README](DeepSigma.DataAccess.RelationalDatabase/README.md#migrations) for design rationale and per-provider DDL details.

## Health checks

Every provider (except `Http`, which has no canonical endpoint to probe) ships an `IHealthChecksBuilder` extension that pairs with `Microsoft.Extensions.Diagnostics.HealthChecks`:

```csharp
services.AddHealthChecks()
    .AddDeepSigmaSqlServer(sqlServerConnStr,         tags: new[] { "readiness" })
    .AddDeepSigmaPostgres(postgresConnStr,           tags: new[] { "readiness" })
    .AddDeepSigmaSqlite("Data Source=app.db",        tags: new[] { "readiness" })
    .AddDeepSigmaMongoDb(mongoConnStr,               tags: new[] { "readiness" })
    .AddDeepSigmaCosmos(endpointUri, apiKey,         tags: new[] { "readiness" })
    .AddDeepSigmaRedis(redisConnStr,                 tags: new[] { "readiness" })
    .AddDeepSigmaAzureBlobStorage(blobConnStr, "documents", tags: new[] { "readiness" });

app.MapHealthChecks("/health");
```

Each check exercises a realistic probe of its backing store (`SELECT 1`, `{ ping: 1 }`, `ReadAccountAsync`, `PING`, container `ExistsAsync`). All checks accept optional `name`, `failureStatus`, `tags`, and `timeout` arguments — see each provider's README for the exact behaviour and recommended defaults.

## Repository layout

```text
Dotnet.DeepSigma.DataAccess/
├── Assets/
│   └── DeepSigma.png                          (shared NuGet package icon)
├── DeepSigma.DataAccess.sln
├── README.md                                  (this file)
├── LICENSE
│
├── DeepSigma.DataAccess.Abstraction/
├── DeepSigma.DataAccess.RelationalDatabase/
├── DeepSigma.DataAccess.SqlServer/
├── DeepSigma.DataAccess.Postgres/
├── DeepSigma.DataAccess.Sqlite/
├── DeepSigma.DataAccess.MongoDB/
├── DeepSigma.DataAccess.Cosmos/
├── DeepSigma.DataAccess.Redis/
├── DeepSigma.DataAccess.AzureBlobStorage/
├── DeepSigma.DataAccess.Http/
│
├── DeepSigma.DataAccess.Abstraction.Tests/
├── DeepSigma.DataAccess.RelationalDatabase.Tests/
├── DeepSigma.DataAccess.SqlServer.Tests/
├── DeepSigma.DataAccess.Postgres.Tests/
├── DeepSigma.DataAccess.Sqlite.Tests/
├── DeepSigma.DataAccess.MongoDB.Tests/
├── DeepSigma.DataAccess.Cosmos.Tests/
├── DeepSigma.DataAccess.AzureBlobStorage.Tests/
└── DeepSigma.DataAccess.Http.Tests/
```

Every package has its own `README.md` covering installation, dependencies, surface area, and a runnable quick-start example. They're linked from the [package matrix](#package-matrix) above and ship with the NuGet package itself.

## Tests

Nine xUnit (v3) test projects, one per package. Most are **unit tests** that run with no external infrastructure (SQLite-backed where a database is needed). A subset are **integration tests** that hit real services — these are tagged `[Trait("Category", "Integration")]` and are excluded by default in the commands below.

| Project | Style | Notes |
|---|---|---|
| `DeepSigma.DataAccess.Abstraction.Tests` | Unit | Contract / model tests. |
| `DeepSigma.DataAccess.RelationalDatabase.Tests` | Unit | `RelationalDatabaseApi`, `RelationalDatabaseTransactionScope`, `MigrationRunner` — all against in-memory SQLite. |
| `DeepSigma.DataAccess.Sqlite.Tests` | Unit | Connection factory, schema service, health check, DI smoke. |
| `DeepSigma.DataAccess.SqlServer.Tests` | Unit + Integration | DI smoke (unit); schema service + `MigrationRunner` (integration, requires local SQL Server with `AutoML` database). |
| `DeepSigma.DataAccess.Postgres.Tests` | Unit + Integration | DI smoke (unit); `MigrationRunner` (integration, requires local PostgreSQL). |
| `DeepSigma.DataAccess.MongoDB.Tests` | Integration | Requires `mongodb://localhost:27017/`. |
| `DeepSigma.DataAccess.Cosmos.Tests` | Unit | DI / contract tests. |
| `DeepSigma.DataAccess.AzureBlobStorage.Tests` | Unit | DI / contract tests. |
| `DeepSigma.DataAccess.Http.Tests` | Unit | Uses a mock `HttpMessageHandler`. |

### Run only the unit tests

```bash
dotnet test DeepSigma.DataAccess.sln --filter "Category!=Integration"
```

### Run everything (requires the local infrastructure noted above)

```bash
dotnet test DeepSigma.DataAccess.sln
```

### Run only the integration tests

```bash
dotnet test DeepSigma.DataAccess.sln --filter "Category=Integration"
```

The `MigrationRunner` integration tests honour `DEEPSIGMA_POSTGRES_CONNECTION` and `DEEPSIGMA_SQLSERVER_CONNECTION` environment variables, so you can point them at your own server without editing the test code.

## Target framework

All packages target **.NET 10.0**.

## License

MIT — see [LICENSE](LICENSE).
