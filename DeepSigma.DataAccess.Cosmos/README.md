# DeepSigma.DataAccess.Cosmos

Azure Cosmos DB helpers for database/container lifecycle, item CRUD, and throughput scaling. Wraps `Microsoft.Azure.Cosmos` with concise methods that take database and container names per call, so a single `CosmosDbApi` instance can service multiple databases and containers.

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

### `CosmosDbApi`

Constructed with the endpoint URI, primary key, and an application name (used by the SDK for telemetry).

| Method | Purpose |
|---|---|
| `CreateDatabaseAsync(databaseId)` | Create the database if it does not exist. |
| `CreateContainerAsync(databaseId, containerId, partitionKeyPath, [throughput])` | Create the container if it does not exist. `partitionKeyPath` is passed without a leading slash. |
| `ScaleContainerAsync(databaseId, containerId, throughputIncrease)` | Adjust the container's manual throughput by the given delta. |
| `InsertAsync<T>(databaseId, containerId, item, idProperty, partitionKeyProperty)` | Read-then-create. Throws if a document with the same id+partition already exists. |
| `QueryItemsAsync<T>(databaseId, containerId, sqlQueryText)` | Run a Cosmos SQL query and materialise all pages. |
| `UpdateItemAsync<T>(databaseId, containerId, newItem, idProperty, partitionKeyProperty)` | Read-then-replace. Throws if the document does not exist. |
| `DeleteItemAsync<T>(databaseId, containerId, id, partitionKeyValue)` | Delete a single item by id and partition-key value. |
| `DeleteDatabaseAndCleanupAsync(databaseId)` | Drop the entire database. |

The `idProperty` and `partitionKeyProperty` parameters are `Expression<Func<T, dynamic>>` selectors. They are resolved at runtime via `DeepSigma.Core.Utilities.ObjectUtilities.GetPropertyValue`, so you pick the properties statically:

```csharp
await cosmos.InsertAsync(
    "AppDb", "Requests",
    new DataRequest { Id = "abc", TenantId = "t-1" },
    idProperty: x => x.Id,
    partitionKeyProperty: x => x.TenantId);
```

## Dependency-injection registration

```csharp
using Microsoft.Extensions.DependencyInjection;

services.AddDeepSigmaCosmos(
    endpointUri: builder.Configuration["Cosmos:EndpointUri"]!,
    apiKey:      builder.Configuration["Cosmos:PrimaryKey"]!,
    appName:     "MyApp");
```

Registers `CosmosDbApi` as a singleton. Because `CosmosDbApi` implements `IDisposable`, the DI container will dispose the underlying `CosmosClient` on application shutdown.

## Quick start

```csharp
using DeepSigma.DataAccess.Cosmos;

var cosmos = new CosmosDbApi(
    endpointUri: "<your endpoint>",
    apiKey:      "<your primary key>",
    appName:     "MyApp");

// 1. Lifecycle
await cosmos.CreateDatabaseAsync("AppDb");
await cosmos.CreateContainerAsync("AppDb", "Requests", partitionKeyPath: "TenantId", throughput: 400);

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

- **Health check.** Pairs with `Microsoft.Extensions.Diagnostics.HealthChecks` via `AddDeepSigmaCosmos(endpointUri, apiKey)` on `IHealthChecksBuilder`. The check calls `ReadAccountAsync` — a lightweight metadata read that does not require any database or container to exist.

  ```csharp
  services.AddHealthChecks()
      .AddDeepSigmaCosmos(endpointUri, apiKey, tags: new[] { "readiness" });
  ```

  A throwaway `CosmosClient` is created per check; the SDK's `ReadAccountAsync` does not accept a `CancellationToken`, so the caller's token is honoured cooperatively only before the request is issued.

- **Client lifetime.** `CosmosDbApi` holds a single long-lived `CosmosClient` for the lifetime of the instance (per Microsoft's guidance) and implements `IDisposable`. Dispose the instance when you are done with it, or register it as a singleton in your DI container.
- **Cancellation.** All methods accept an optional `CancellationToken`. It is forwarded to the underlying Cosmos SDK calls.
- **Insert semantics.** `InsertAsync` issues a single `CreateItemAsync` call and translates a Cosmos 409 Conflict into `CosmosDuplicateItemException`. No pre-read — race-free.
- **Update / delete semantics.** `UpdateItemAsync` and `DeleteItemAsync` each issue a single call and translate a Cosmos 404 Not Found into `CosmosItemNotFoundException`. No pre-read — race-free.
- **Partition keys.** Both expression-based selectors (`InsertAsync`, `UpdateItemAsync`) and the explicit `object?` parameter (`DeleteItemAsync`) support the full set of Cosmos partition-key types: `string`, `bool`, `double` / `float` / `int` / `long` / `decimal`, or `null` (which maps to `PartitionKey.None`). Unsupported types throw `ArgumentException`.
- **Throughput scaling** uses `ReadThroughputAsync` / `ReplaceThroughputAsync`. Autoscale containers do not expose manual throughput; `ScaleContainerAsync` will throw `InvalidOperationException` in that case.
- **Exceptions.** Domain-specific exceptions live in `DeepSigma.DataAccess.Cosmos.Exceptions`: `CosmosDuplicateItemException`, `CosmosItemNotFoundException`. Both wrap the underlying `CosmosException` for diagnostics.

## License

MIT
