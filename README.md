# DeepSigma.DataAccess

A modular family of .NET data-access packages for relational databases, document stores, caches, blob storage, and HTTP APIs. Each storage technology is shipped as its own NuGet package so consumers only pull in the dependencies they actually use.

This repository contains 9 published packages plus shared abstractions. They target **.NET 10.0** and are MIT-licensed.

## Why modular packages?

The previous `DeepSigma.DataAccess` (1.x) was a single package that bundled Azure SDKs, MongoDB, SQL Server, Postgres, Redis, and HTTP utilities together. Consumers needing only Postgres ended up with Cosmos, Mongo, and Redis transitively.

The 2.x layout splits each storage concern into its own package, with a small shared `Abstraction` package holding interfaces and models. Provider packages depend only on what they need.

## Package matrix

| Package | Purpose | Key dependency |
|---|---|---|
| [DeepSigma.DataAccess.Abstraction](DeepSigma.DataAccess.Abstraction/README.md) | Shared interfaces (`IDbConnectionFactory`, `IDatabaseSchemaService`) and schema models (`TableName`, `TableField`, `TableConstraint`, `TableForeignKey`). | _(none)_ |
| [DeepSigma.DataAccess.RelationalDatabase](DeepSigma.DataAccess.RelationalDatabase/README.md) | Shared Dapper-backed `RelationalDatabaseAPI`. Pair with a provider package. | Dapper |
| [DeepSigma.DataAccess.SqlServer](DeepSigma.DataAccess.SqlServer/README.md) | SQL Server connection factory + schema service. | Microsoft.Data.SqlClient |
| [DeepSigma.DataAccess.Postgres](DeepSigma.DataAccess.Postgres/README.md) | PostgreSQL connection factory + schema service. | Npgsql |
| [DeepSigma.DataAccess.MongoDB](DeepSigma.DataAccess.MongoDB/README.md) | MongoDB CRUD helpers built around an `IMongoDocument` contract. | MongoDB.Driver |
| [DeepSigma.DataAccess.Cosmos](DeepSigma.DataAccess.Cosmos/README.md) | Azure Cosmos DB helpers for databases, containers, items, and throughput. | Microsoft.Azure.Cosmos, DeepSigma.Core |
| [DeepSigma.DataAccess.Redis](DeepSigma.DataAccess.Redis/README.md) | Redis cache get/set/remove helpers. | StackExchange.Redis, Newtonsoft.Json |
| [DeepSigma.DataAccess.AzureBlobStorage](DeepSigma.DataAccess.AzureBlobStorage/README.md) | Azure Blob Storage upload, download, delete, and list. | Azure.Storage.Blobs |
| [DeepSigma.DataAccess.Http](DeepSigma.DataAccess.Http/README.md) | Helpers for fetching and deserializing JSON / CSV API payloads. | DeepSigma.DataAccess.CsvUtilities |

## Dependency graph

```
                          Abstraction
                         /     |
                        /      |
        RelationalDatabase     |
            /        \         |
       SqlServer    Postgres   |
                               |
   (independent leaves)        |
   MongoDB                     |
   Cosmos ─── DeepSigma.Core   |
   Redis                       |
   AzureBlobStorage            |
   Http ───── DeepSigma.DataAccess.CsvUtilities
```

- **Abstraction** has no dependencies.
- **RelationalDatabase** depends on Abstraction + Dapper.
- **SqlServer** and **Postgres** depend on RelationalDatabase (and transitively Abstraction).
- **MongoDB**, **Cosmos**, **Redis**, **AzureBlobStorage**, and **Http** are independent of the relational stack — they each ship only their own driver dependency.

## Choosing what to install

Pick the packages that match the stores you use:

- "I just want to call Postgres with Dapper" → install **DeepSigma.DataAccess.Postgres** (pulls Abstraction + RelationalDatabase + Npgsql).
- "I just want SQL Server schema discovery" → install **DeepSigma.DataAccess.SqlServer**.
- "I just need Mongo CRUD" → install **DeepSigma.DataAccess.MongoDB**.
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

var db = new RelationalDatabaseAPI(factory);

var users = await db.GetAllAsync<dynamic>("SELECT id, name FROM users WHERE active = TRUE");
```

To swap to SQL Server, change the factory and the connection string — `RelationalDatabaseAPI` stays the same:

```csharp
IDbConnectionFactory factory = new SqlServerConnectionFactory(
    "Server=localhost;Database=AppDb;Integrated Security=True;TrustServerCertificate=True;");
```

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
├── DeepSigma.DataAccess.MongoDB/
├── DeepSigma.DataAccess.Cosmos/
├── DeepSigma.DataAccess.Redis/
├── DeepSigma.DataAccess.AzureBlobStorage/
├── DeepSigma.DataAccess.Http/
│
├── DeepSigma.DataAccess.MongoDB.Tests/
└── DeepSigma.DataAccess.SqlServer.Tests/
```

Every package has its own `README.md` covering installation, dependencies, surface area, and a runnable quick-start example. They're linked from the [package matrix](#package-matrix) above and ship with the NuGet package itself.

## Tests

Two xUnit-based test projects:

- `DeepSigma.DataAccess.MongoDB.Tests` — exercises the MongoDB CRUD helpers against `mongodb://localhost:27017/`.
- `DeepSigma.DataAccess.SqlServer.Tests` — exercises the SQL Server schema service against a local SQL Server instance with a database named `AutoML`.

Both are integration tests that hit real local infrastructure. Provision the services (or update the connection strings) before running:

```bash
dotnet test DeepSigma.DataAccess.sln
```

## Target framework

All packages target **.NET 10.0**.

## License

MIT — see [LICENSE](LICENSE).
