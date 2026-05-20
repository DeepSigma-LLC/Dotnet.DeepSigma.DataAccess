# DeepSigma.DataAccess.Cosmos

Azure Cosmos DB helpers for database/container lifecycle, item CRUD, and throughput scaling. Wraps `Microsoft.Azure.Cosmos` with concise methods that take database and container names per call, so a single `CosmosDBAPI` instance can service multiple databases and containers.

This package is **independent** of the relational stack.

## Installation

```bash
dotnet add package DeepSigma.DataAccess.Cosmos
```

## Dependencies

| Package | Purpose |
|---|---|
| `Microsoft.Azure.Cosmos` | Official Azure Cosmos DB .NET SDK. |
| `DeepSigma.Core` | Provides `ObjectUtilities.GetPropertyValue` used to extract id and partition-key values from items via expression trees. |

## What it provides

### `CosmosDBAPI`

Constructed with the endpoint URI, primary key, and an application name (used by the SDK for telemetry).

| Method | Purpose |
|---|---|
| `CreateDatabaseAsync(databaseId)` | Create the database if it does not exist. |
| `CreateContainerAsync(databaseId, containerId, partitionKeyPath, [throughput])` | Create the container if it does not exist. `partitionKeyPath` is passed without a leading slash. |
| `ScaleContainerAsync(databaseId, containerId, throughputIncrease)` | Adjust the container's manual throughput by the given delta. |
| `InsertAsync<T>(databaseId, containerId, item, idProperty, partitionKeyProperty)` | Read-then-create. Throws if a document with the same id+partition already exists. |
| `QueryItemsAsync<T>(databaseId, containerId, sqlQueryText)` | Run a Cosmos SQL query and materialise all pages. |
| `UpdateItemAsync<T>(databaseId, containerId, newItem, idProperty, partitionKeyProperty)` | Read-then-replace. Throws if the document does not exist. |
| `DeleteItemAsync<T>(databaseId, containerId, id, partitionKeyProperty)` | Delete a single item. |
| `DeleteDatabaseAndCleanupAsync(databaseId)` | Drop the entire database. |

The `idProperty` and `partitionKeyProperty` parameters are `Expression<Func<T, dynamic>>` selectors. They are resolved at runtime via `DeepSigma.Core.Utilities.ObjectUtilities.GetPropertyValue`, so you pick the properties statically:

```csharp
await cosmos.InsertAsync(
    "AppDb", "Requests",
    new DataRequest { Id = "abc", TenantId = "t-1" },
    idProperty: x => x.Id,
    partitionKeyProperty: x => x.TenantId);
```

## Quick start

```csharp
using DeepSigma.DataAccess.Cosmos;

var cosmos = new CosmosDBAPI(
    end_point_uri: "<your endpoint>",
    api_key:        "<your primary key>",
    app_name:       "MyApp");

// 1. Lifecycle
await cosmos.CreateDatabaseAsync("AppDb");
await cosmos.CreateContainerAsync("AppDb", "Requests", partitionKey: "TenantId", throughput: 400);

// 2. Insert
public sealed class DataRequest
{
    public string Id { get; set; } = "";
    public string TenantId { get; set; } = "";
    public string Name { get; set; } = "";
}

var item = new DataRequest { Id = Guid.NewGuid().ToString(), TenantId = "tenant-1", Name = "First" };
await cosmos.InsertAsync("AppDb", "Requests", item, x => x.Id, x => x.TenantId);

// 3. Query
List<DataRequest> matches = await cosmos.QueryItemsAsync<DataRequest>(
    "AppDb", "Requests",
    "SELECT * FROM c WHERE c.TenantId = 'tenant-1'");

// 4. Update
item.Name = "Renamed";
await cosmos.UpdateItemAsync("AppDb", "Requests", item, x => x.Id, x => x.TenantId);
```

## Notes

- **Client lifetime.** Each method instantiates a `CosmosClient` inside a `using` block. For heavy workloads this adds latency and connection setup cost on every call; the official guidance is to keep one `CosmosClient` for the lifetime of the application. Refactoring to a long-lived client is a candidate future change.
- **Insert semantics.** `InsertAsync` first reads the document and throws if it already exists. If you want true upsert semantics, use `UpdateItemAsync` after your own check, or call `QueryItemsAsync` first.
- **Partition keys are passed as strings.** The expression selectors are evaluated to a string via `ObjectUtilities.GetPropertyValue<T, string>`. Non-string partition keys (numeric, boolean) are not currently supported by these helpers.
- **Throughput scaling** uses `ReadThroughputAsync` / `ReplaceThroughputAsync`. If the container is configured for autoscale, manual replacement may fail — read the SDK docs before calling `ScaleContainerAsync` on an autoscale container.

## License

MIT
