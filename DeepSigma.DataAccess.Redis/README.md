# DeepSigma.DataAccess.Redis

Small Redis cache helper: get, set with expiry, and remove. Wraps `StackExchange.Redis` and uses `Newtonsoft.Json` to serialise/deserialise values transparently.

This package is **independent** of the relational stack.

## Installation

```bash
dotnet add package DeepSigma.DataAccess.Redis
```

## Dependencies

| Package | Purpose |
|---|---|
| `StackExchange.Redis` | Redis client. |
| `Newtonsoft.Json` | JSON serialisation for cached values. |

## What it provides

### `RedisCacheApi`

Constructed with a Redis connection string and an instance name (currently stored for future namespacing — not yet used in keys). The constructor connects synchronously and holds the resulting `IDatabase` for subsequent calls.

| Method | Returns | Behaviour |
|---|---|---|
| `GetCacheData<T>(key)` | `T?` | Reads the string at `key`, returns `default` if empty, otherwise deserialises via `JsonConvert`. |
| `SetCacheData<T>(key, value, expirationTime)` | `bool` | Serialises `value` to JSON and stores it with a TTL derived from `expirationTime - DateTime.Now`. |
| `RemoveCacheData(key)` | `object` | If the key exists, deletes it and returns the driver's `bool` result; otherwise returns `false`. |

## Dependency-injection registration

```csharp
using Microsoft.Extensions.DependencyInjection;

services.AddDeepSigmaRedis(
    connectionString: builder.Configuration.GetConnectionString("Redis")!,
    instanceName:     "myapp");
```

Registers `RedisCacheApi` as a singleton, so the long-lived `ConnectionMultiplexer` is shared across the application.

## Quick start

```csharp
using DeepSigma.DataAccess.Redis;

var cache = new RedisCacheApi("localhost:6379", redisInstanceName: "myapp");

// Set
await cache.SetCacheData(
    key: "user:42",
    value: new { Name = "Ada", Email = "ada@example.com" },
    expirationTime: DateTimeOffset.UtcNow.AddMinutes(30));

// Get
var user = await cache.GetCacheData<UserDto>("user:42");

// Remove
await cache.RemoveCacheData("user:42");
```

## Health checks

Pairs with the `Microsoft.Extensions.Diagnostics.HealthChecks` system. The check connects and runs `PING`.

```csharp
services.AddHealthChecks()
    .AddDeepSigmaRedis("localhost:6379", tags: new[] { "readiness" });

app.MapHealthChecks("/health");
```

Optional arguments (all named): `name` (default `"deepsigma_redis"`), `failureStatus` (default `Unhealthy`), `tags`, `timeout` (recommended: `TimeSpan.FromSeconds(5)`).

A throwaway `ConnectionMultiplexer` is created per check, so the check exercises the full connect-and-handshake path on every invocation. For very high-frequency probes (sub-second), consider a custom check that reuses the singleton multiplexer registered by `AddDeepSigmaRedis(...)`.

## Notes

- **TTL calculation.** `SetCacheData` computes the TTL as `expirationTime.DateTime.Subtract(DateTime.Now)`. Pass a `DateTimeOffset` in a comparable kind (UTC vs local) to avoid surprises — and pass a value in the future, otherwise the TTL is negative and the key may behave unexpectedly.
- **Connection lifetime.** The `ConnectionMultiplexer` is created in the constructor and held for the lifetime of the `RedisCacheApi` instance. Per the `StackExchange.Redis` docs, the multiplexer is designed to be long-lived and shared across the application — so reuse a single `RedisCacheApi` instead of constructing one per call.
- **Instance name.** The `redisInstanceName` constructor parameter is currently stored but not applied to keys. If you need key namespacing, prefix your keys explicitly (e.g. `$"{instance}:user:42"`).
- **Serialisation format.** Values are stored as Newtonsoft JSON strings. Reads only succeed for types that round-trip cleanly through `JsonConvert.SerializeObject` / `DeserializeObject`.
- **Cancellation.** All methods accept an optional `CancellationToken`, but `StackExchange.Redis` does not honour `CancellationToken` on its async methods — it uses `CommandFlags` internally. The token is checked via `ThrowIfCancellationRequested()` before each command, so cooperative cancellation works *before* the call is issued; once a command is in-flight to the server, it cannot be cancelled.

## License

MIT
