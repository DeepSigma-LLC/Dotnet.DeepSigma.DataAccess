# DeepSigma.DataAccess

A general-purpose .NET data access library that bundles common patterns for working with relational databases, MongoDB, Azure Cosmos DB, Azure Blob Storage, Redis, and remote API payloads in one package.

## Features

- Relational database access for **SQL Server** and **PostgreSQL** via Dapper
- SQL Server schema inspection helpers for:
  - tables
  - table fields
  - constraints
  - foreign keys
- MongoDB CRUD helpers built around an `IMongoDocument` contract
- Azure Cosmos DB helpers for databases, containers, inserts, queries, updates, deletes, and throughput scaling
- Azure Blob Storage upload, download, delete, and list operations
- Redis cache get, set, and remove helpers
- API utilities for downloading and deserializing **JSON** and **CSV**
- XML serialization and deserialization helpers

## Target framework

This package targets **.NET 10.0**.

## Installation

### NuGet

```bash
dotnet add package DeepSigma.DataAccess
```

### Project reference

```xml
<ProjectReference Include="..\Dotnet.DeepSigma.DataAccess\DeepSigma.DataAccess.csproj" />
```

## Package dependencies

The library references these primary packages:

- Azure.Storage.Blobs
- CsvHelper
- Dapper
- DeepSigma.General
- Microsoft.Azure.Cosmos
- Microsoft.Data.SqlClient
- MongoDB.Driver
- Newtonsoft.Json
- Npgsql
- StackExchange.Redis

## Project structure

```text
Dotnet.DeepSigma.DataAccess/
├── Dotnet.DeepSigma.DataAccess/
│   ├── API/
│   │   └── APIUtilities.cs
│   ├── Database/
│   │   ├── BlobStorageAPI.cs
│   │   ├── CosmosDBAPI.cs
│   │   ├── DatabaseAPI.cs
│   │   ├── MongoDBAPI.cs
│   │   ├── RedisCacheAPI.cs
│   │   ├── RelationalDatabaseType.cs
│   │   ├── SQLServerDatabaseSchemaService.cs
│   │   └── SQL/
│   ├── Models/
│   │   └── IMongoDocument.cs
│   ├── Utilities/
│   │   ├── CsvUtilities.cs
│   │   ├── ObjectUtilities.cs
│   │   └── XMLUtilities.cs
│   └── DeepSigma.DataAccess.csproj
└── DataAccessTests/
    ├── Models/
    │   └── DataRequest.cs
    ├── Tests/
    │   ├── MongoDB_Tests.cs
    │   └── SQLDatabaseSchema_Tests.cs
    └── DeepSigmaa.DataAccess.Tests.csproj
```

## Usage

### Relational databases with Dapper

`DatabaseAPI` wraps Dapper and creates connections for either SQL Server or PostgreSQL.

```csharp
using DeepSigma.DataAccess.Database;
using Dapper;

var api = new DatabaseAPI(
    connection_string: "Server=localhost;Database=AppDb;Trusted_Connection=True;",
    database_type: RelationalDatabaseType.SQLServer
);

var users = await api.GetAllAsync<dynamic>(
    "SELECT Id, Name FROM Users WHERE IsActive = @IsActive",
    new DynamicParameters(new { IsActive = true })
);
```

### SQL Server schema discovery

`SQLServerDatabaseSchemaService` reads packaged SQL files and exposes helpers for schema metadata.

```csharp
using DeepSigma.DataAccess.Database;

var schema = new SQLServerDatabaseSchemaService(
    "Data Source=localhost;Database=MyDb;Integrated Security=True;TrustServerCertificate=True;"
);

var tables = await schema.GetTables();
var fields = await schema.GetTableFields();
var constraints = await schema.GetConstraints();
var foreignKeys = await schema.GetForiegnKeys();
```

### MongoDB

MongoDB documents implement `IMongoDocument`, which requires an `Id` property.

```csharp
using DeepSigma.DataAccess.Database;
using DeepSigma.DataAccess.Models;
using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

public class DataRequest : IMongoDocument
{
    public string Name { get; set; } = "";
    public string Description { get; set; } = "";

    [BsonId]
    [BsonRepresentation(BsonType.ObjectId)]
    public string Id { get; set; } = "";
}

var mongo = new MongoDBAPI("mongodb://localhost:27017/");

await mongo.InsertAsync("TestDB", "Requests", new DataRequest
{
    Id = "68d0a9d8ad3512e0e907c0cb",
    Name = "Test1",
    Description = "A test description"
});

var item = await mongo.GetByIdAsync<DataRequest>(
    "TestDB",
    "Requests",
    "68d0a9d8ad3512e0e907c0cb"
);
```

### Azure Cosmos DB

```csharp
using DeepSigma.DataAccess.Database;

var cosmos = new CosmosDBAPI(
    end_point_uri: "<endpoint>",
    api_key: "<key>",
    app_name: "DeepSigmaApp"
);

await cosmos.CreateDatabaseAsync("AppDb");
await cosmos.CreateContainerAsync("AppDb", "Requests", "tenantId", throughput: 400);
```

### Azure Blob Storage

```csharp
using DeepSigma.DataAccess.Database;

var blobs = new BlobStorageAPI("<connection-string>", "documents");

await blobs.UploadToBlob("report.pdf", allowOverwrite: true);
var names = await blobs.ListAllItemsBlobs();
await blobs.DownloadFromBlob("report.pdf", "downloads/report.pdf");
```

### Redis cache

```csharp
using DeepSigma.DataAccess.Database;

var cache = new RedisCacheAPI("<redis-connection>", "app");

await cache.SetCacheData("user:42", new { Name = "Ada" }, DateTimeOffset.UtcNow.AddMinutes(30));
var value = await cache.GetCacheData<dynamic>("user:42");
await cache.RemoveCacheData("user:42");
```

### Fetching JSON and CSV from APIs

```csharp
using DeepSigma.DataAccess.API;

var weather = await APIUtilities.GetDataFromURLAsync<MyDto>(
    "https://example.com/data.json"
);

var rows = await APIUtilities.GetDataFromCSVAsync<MyCsvRow>(
    "https://example.com/data.csv"
);
```

### XML serialization

```csharp
using DeepSigma.DataAccess.Utilities;

var xml = XMLUtilities.Serialize(myObject);
var restored = XMLUtilities.GetObject<MyType>("my-file.xml");
```

## Tests

The repository includes an xUnit-based test project covering:

- MongoDB insert, delete, delete-many, count, find, and get-by-id flows
- SQL Server schema service methods for tables, constraints, foreign keys, and fields

The current tests are written as integration-style tests and expect local infrastructure such as:

- MongoDB on `mongodb://localhost:27017/`
- SQL Server on `localhost` with a database named `AutoML`

## Running tests

```bash
dotnet test Dotnet.DeepSigma.DataAccess/DeepSigma.DataAccess.sln
```

Because the test suite relies on local services and specific connection details, you may need to update connection strings or provision local databases before running it successfully.

## Notes

- The library generates a NuGet package on build.
- SQL schema query files are included in the package output so `SQLServerDatabaseSchemaService` can load them at runtime.
- The package icon is stored at `Assets/DeepSigma.png`.

## License

MIT
