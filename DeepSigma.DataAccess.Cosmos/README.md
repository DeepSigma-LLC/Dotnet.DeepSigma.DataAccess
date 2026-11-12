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
| `InsertAsync<T>(databaseId, containerId, item, idProperty, partitionKeyProperty)` | Create an item; returns `CosmosInsertResult<T>`. |
| `QueryItemsAsync<T>(databaseId, containerId, sqlQueryText)` | Run a Cosmos SQL query and materialise all pages. |
| `UpdateItemAsync<T>(databaseId, containerId, newItem, idProperty, partitionKeyProperty)` | Replace an item; returns `CosmosUpdateResult<T>`. |
| `DeleteItemAsync<T>(databaseId, containerId, id, partitionKeyValue)` | Delete an item; returns `CosmosDeleteResult`. |
| `DeleteDatabaseAndCleanupAsync(databaseId)` | Drop the entire database. |

The `idProperty` and `partitionKeyProperty` parameters are `Expression<Func<T, dynamic>>` selectors. They are resolved at runtime via `DeepSigma.Core.Utilities.ObjectUtilities.GetPropertyValue`, so you pick the properties statically:

```csharp
using DeepSigma.DataAccess.Cosmos.Results;

CosmosInsertResult<DataRequest> result = await cosmos.InsertAsync(
    "AppDb", "Requests",
    new DataRequest { Id = "abc", TenantId = "t-1" },
    idProperty: x => x.Id,
    partitionKeyProperty: x => x.TenantId);

DataRequest created = result switch
{
    CosmosCreated<DataRequest> success => success.Item,
    CosmosConflict conflict => throw new InvalidOperationException("Insert conflicted", conflict.Exception),
    null => throw new InvalidOperationException("Uninitialized insert result"),
};
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
using DeepSigma.DataAccess.Cosmos.Results;

using var cosmos = new CosmosDbApi(
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
CosmosInsertResult<DataRequest> insert = await cosmos.InsertAsync("AppDb", "Requests", item, x => x.Id, x => x.TenantId);
item = insert switch
{
    CosmosCreated<DataRequest> created => created.Item,
    CosmosConflict conflict => throw new InvalidOperationException("Insert conflicted", conflict.Exception),
    null => throw new InvalidOperationException("Uninitialized insert result"),
};

// 3. Query
List<DataRequest> matches = await cosmos.QueryItemsAsync<DataRequest>(
    "AppDb", "Requests",
    "SELECT * FROM c WHERE c.TenantId = 'tenant-1'");

// 4. Update
item.Name = "Renamed";
CosmosUpdateResult<DataRequest> update = await cosmos.UpdateItemAsync("AppDb", "Requests", item, x => x.Id, x => x.TenantId);
item = update switch
{
    CosmosUpdated<DataRequest> updated => updated.Item,
    CosmosResourceNotFound missing => throw new InvalidOperationException("Resource not found", missing.Exception),
    null => throw new InvalidOperationException("Uninitialized update result"),
};

// 5. Delete
CosmosDeleteResult delete = await cosmos.DeleteItemAsync<DataRequest>("AppDb", "Requests", item.Id, item.TenantId);
string deleteMessage = delete switch
{
    CosmosDeleted => "Deleted",
    CosmosResourceNotFound missing => $"Resource not found: {missing.ContainerId}",
    null => throw new InvalidOperationException("Uninitialized delete result"),
};
```

## Migrating from 2.x

`InsertAsync`, `UpdateItemAsync`, and `DeleteItemAsync` now return unions in `DeepSigma.DataAccess.Cosmos.Results`. Switch on each result: success contains the returned item (or a deletion confirmation), while conflict/not-found contains the database, container, item ID, and original `CosmosException` in `Exception`. Code that previously caught `CosmosDuplicateItemException` or `CosmosItemNotFoundException` must handle these cases instead. The old exception types remain available, but these methods no longer throw them.

**Do not ignore the returned result:** an `await` whose return value is discarded may still compile, but will no longer throw for these expected responses. Switch expressions are checked for missing cases, normally with warnings; configure the relevant warnings as errors if exhaustive handling must be enforced. Generated unions are structs: `default` can contain null, so handle a `null` arm where uninitialized values may reach a switch. Avoid a catch-all arm if you want warnings when cases are added.

Only status 409 for insert and status 404 for update/delete with substatus 0 are mapped. A conflict can have causes other than a duplicate ID, including unique-key or partition-key conflicts; a not-found response can refer to the item or a parent resource. Inspect the original exception and diagnostics before deciding how to respond. Other status/substatus combinations, invalid arguments, SDK failures and cancellation still propagate as exceptions.

References: [Cosmos DB 404 troubleshooting](https://learn.microsoft.com/en-us/azure/cosmos-db/troubleshoot-not-found), [409 troubleshooting](https://learn.microsoft.com/en-us/azure/cosmos-db/troubleshoot-conflict), and [HTTP status codes](https://learn.microsoft.com/en-us/rest/api/cosmos-db/http-status-codes-for-cosmosdb).

## Notes

- **Health check.** Pairs with `Microsoft.Extensions.Diagnostics.HealthChecks` via `AddDeepSigmaCosmos(endpointUri, apiKey)` on `IHealthChecksBuilder`. The check calls `ReadAccountAsync` — a lightweight metadata read that does not require any database or container to exist.

  ```csharp
  services.AddHealthChecks()
      .AddDeepSigmaCosmos(endpointUri, apiKey, tags: new[] { "readiness" });
  ```

  A throwaway `CosmosClient` is created per check; the SDK's `ReadAccountAsync` does not accept a `CancellationToken`, so the caller's token is honoured cooperatively only before the request is issued.

- **Client lifetime.** `CosmosDbApi` holds a single long-lived `CosmosClient` for the lifetime of the instance (per Microsoft's guidance) and implements `IDisposable`. Dispose the instance when you are done with it, or register it as a singleton in your DI container.
- **Cancellation.** All methods accept an optional `CancellationToken`. It is forwarded to the underlying Cosmos SDK calls.
- **Insert semantics.** `InsertAsync` issues a single `CreateItemAsync` call and returns a `CosmosConflict` for a 409 with substatus 0. No pre-read — race-free.
- **Update / delete semantics.** `UpdateItemAsync` and `DeleteItemAsync` each issue a single call and return a `CosmosResourceNotFound` for a 404 with substatus 0. No pre-read — race-free.
- **Partition keys.** Both expression-based selectors (`InsertAsync`, `UpdateItemAsync`) and the explicit `object?` parameter (`DeleteItemAsync`) support the full set of Cosmos partition-key types: `string`, `bool`, `double` / `float` / `int` / `long` / `decimal`, or `null` (which maps to `PartitionKey.None`). Unsupported types throw `ArgumentException`.
- **Throughput scaling** uses `ReadThroughputAsync` / `ReplaceThroughputAsync`. Autoscale containers do not expose manual throughput; `ScaleContainerAsync` will throw `InvalidOperationException` in that case.
- **Other errors.** Failures not represented by the result cases propagate as exceptions; see [Migrating from 2.x](#migrating-from-2x).

## License

MIT
