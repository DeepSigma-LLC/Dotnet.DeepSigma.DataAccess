# DeepSigma.DataAccess.MongoDB

MongoDB CRUD helpers built around an `IMongoDocument` contract. Wraps the official `MongoDB.Driver` with concise async methods for the common single- and multi-document operations.

This package is **independent** of the relational stack — it does not depend on `Abstraction`, `RelationalDatabase`, or any SQL driver.

## Installation

```bash
dotnet add package DeepSigma.DataAccess.MongoDB
```

## Dependencies

| Package | Purpose |
|---|---|
| `MongoDB.Driver` | Official MongoDB .NET driver. |

## What it provides

### `IMongoDocument`

Marker interface for documents that have a string `Id` mapped to a Mongo `ObjectId`:

```csharp
public interface IMongoDocument
{
    [BsonId]
    [BsonRepresentation(BsonType.ObjectId)]
    string Id { get; set; }
}
```

Several `MongoDBAPI` methods (`GetByIdAsync`, `ReplaceAsync`, `DeleteAsync`) constrain `T` to `IMongoDocument` so they can target documents by `Id`. Other methods (`FindAsync`, `CountAsync`, `InsertAsync`, `DeleteManyAsync`, `CreateIndexAsync`) are unconstrained and work on any document type.

### `MongoDBAPI`

Constructed with a connection string; internally creates and reuses a single `MongoClient`. Each method takes the database and collection names as parameters so a single `MongoDBAPI` instance can service multiple collections.

| Method | Returns | Notes |
|---|---|---|
| `GetByIdAsync<T>(db, collection, id, [ct])` | `T?` | `T : IMongoDocument`. Returns `null` if not found. |
| `FindAsync<T>(db, collection, [filter], [sort], skip, take, [ct])` | `IReadOnlyList<T>` | Pages with `Skip` + `Limit`; default page is `[0, 100)`. |
| `CountAsync<T>(db, collection, [filter], [ct])` | `long` | Counts documents matching the filter (empty filter counts all). |
| `InsertAsync<T>(db, collection, document, [ct])` | `Task` | Single insert. |
| `InsertAsync<T>(db, collection, documents, [ct])` | `Task` | Bulk insert via `InsertManyAsync`. |
| `ReplaceAsync<T>(db, collection, document, upsert, [ct])` | `bool` | `T : IMongoDocument`. Returns `true` on a successful modify or upsert. |
| `DeleteAsync<T>(db, collection, id, [ct])` | `bool` | `T : IMongoDocument`. Returns `true` if a document was deleted. |
| `DeleteManyAsync<T>(db, collection, filter, [ct])` | `bool` | Returns `true` if at least one document was deleted. |
| `CreateIndexAsync<T>(db, collection, keys, [options], [ct])` | `string` | Returns the created index name. |

All methods accept a `CancellationToken` and use the driver's async APIs throughout.

## Quick start

```csharp
using DeepSigma.DataAccess.MongoDB;
using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using MongoDB.Driver;

// 1. Define a document
public sealed class DataRequest : IMongoDocument
{
    [BsonId]
    [BsonRepresentation(BsonType.ObjectId)]
    public string Id { get; set; } = "";

    public string Name { get; set; } = "";
    public string Description { get; set; } = "";
}

// 2. Construct the API
var mongo = new MongoDBAPI("mongodb://localhost:27017/");

// 3. Insert
await mongo.InsertAsync("AppDb", "Requests",
    new DataRequest { Id = "68d0a9d8ad3512e0e907c0cb", Name = "First", Description = "Hello" });

// 4. Find by id
DataRequest? doc = await mongo.GetByIdAsync<DataRequest>("AppDb", "Requests", "68d0a9d8ad3512e0e907c0cb");

// 5. Query with a filter
IReadOnlyList<DataRequest> matches = await mongo.FindAsync(
    "AppDb", "Requests",
    Builders<DataRequest>.Filter.Where(x => x.Name == "First"));

// 6. Replace (with upsert)
await mongo.ReplaceAsync("AppDb", "Requests",
    new DataRequest { Id = "68d0a9d8ad3512e0e907c0cb", Name = "Renamed", Description = "Updated" },
    upsert: true);

// 7. Delete
await mongo.DeleteAsync<DataRequest>("AppDb", "Requests", "68d0a9d8ad3512e0e907c0cb");
```

## Notes

- The `MongoClient` is created once per `MongoDBAPI` instance. The Mongo driver is thread-safe and pools connections internally, so it is fine — and recommended — to share a single `MongoDBAPI` instance across your application.
- `Id` must be a 24-character hex string parseable as a `BsonObjectId` because of the `BsonRepresentation(BsonType.ObjectId)` mapping. If you need different id strategies, define your own document type without `IMongoDocument` and use the unconstrained methods (`FindAsync`, `InsertAsync(...)`).
- `DeleteManyAsync` returns `true` only if at least one document was deleted; it does **not** indicate the count of deletions.

## License

MIT
