# Proposal: `DeepSigma.DataAccess.Http` → a unified web-communication package

**Status:** Draft for review
**Scope:** HTTP (existing), Server-Sent Events (placeholder), WebSockets (placeholder)
**Target:** `net11.0` (C# 15), rename `DeepSigma.DataAccess.Http` → `DeepSigma.DataAccess.Web` (currently v2.0.0 → 3.0.0)

---

## 1. Where the project stands today

| Area | File | State |
|---|---|---|
| Request/response HTTP | `HttpApi.cs` | **Mature.** JSON/CSV/XML reads, `POST`/`PUT`/`PATCH`/`DELETE`, form bodies, streaming downloads, streaming XML, `SendAsync` escape hatch. Per-call timeout via linked CTS, `EnsureSuccessStatusCode`, logging hook. |
| Serialization | `Serializer.cs` | **Mature.** JSON (default + snake_case presets), CSV, XML static helpers. |
| Resilience | `Throttling/MinIntervalDelegatingHandler.cs`, `Throttling/RetryAfterDelegatingHandler.cs` | **Mature.** Min-interval pacing + `Retry-After`/backoff, both `TimeProvider`-testable and composable via `IHttpClientBuilder`. |
| DI | `ServiceCollectionExtensions.cs` | `AddDeepSigmaHttp()`, `AddMinIntervalThrottle()`, `AddRetryAfterPolicy()`. |
| **SSE** | `ServerSideEventApi.cs` | **Empty placeholder.** `internal class ServerSideEventAPI {}` — also mis-cased (`API` vs `Api`) and undocumented. |
| **WebSockets** | `WebSocketApi.cs` | **Empty placeholder.** `public class WebSocketApi {}`, empty XML doc. |

The HTTP layer is genuinely good — it's the template for quality the other two should match. The gap is entirely SSE and WebSockets.

---

## 2. The core architectural tension

`HttpApi` is **stateless**: every method is a self-contained request/response. That model is correct for HTTP and should not change.

SSE and WebSockets are the opposite — **stateful, long-lived connections** with a lifecycle: connect → stream/exchange → reconnect-on-drop → close. Modeling them as another `HttpApi`-style bag of methods would be a mistake. They need:

- a **connection object** that owns the socket/stream and implements `IAsyncDisposable`,
- **connection-state** semantics (Connecting / Open / Reconnecting / Closed),
- **reconnection with backoff**, and (for SSE) `Last-Event-ID` resumption,
- back-pressure-aware delivery (`IAsyncEnumerable<T>` and/or `System.Threading.Channels`).

So the recommendation is **not** "add two more methods to a utility class" — it's "add two small, focused connection APIs that share this package's serialization and resilience primitives."

---

## 3. Recommended changes

### 3.1 Rename the package to `DeepSigma.DataAccess.Web` (decided)

The name `…Http` undersells a package that now owns HTTP **+ SSE + WebSockets** (and ws:// isn't HTTP). Rename:

- **Package ID / assembly / `RootNamespace`:** `DeepSigma.DataAccess.Http` → `DeepSigma.DataAccess.Web`.
- **Namespaces:** `DeepSigma.DataAccess.Http` → `DeepSigma.DataAccess.Web` (and `.Throttling` → `.Web.Resilience` or keep `.Throttling`). Bundle this with the 3.0.0 major so the breaking `using` change lands once, alongside the new capabilities.
- **Old package:** push one final `DeepSigma.DataAccess.Http` build marked `<PackageDescription>` "moved to DeepSigma.DataAccess.Web" and mark it deprecated/unlisted on the feed, so existing consumers get a pointer rather than a dead end.
- **Version string:** fix `<Version>2.0.0.0</Version>` → semantic `3.0.0` (NuGet is 3-part SemVer; the 4-part form is an artifact).

A type-forwarding shim assembly is possible but rarely worth it for an internal package — a documented `using` find-and-replace is simpler.

### 3.2 Introduce interfaces for all three clients

Today `HttpApi` is a concrete class, so consumers can't mock it without a stub `HttpMessageHandler`. As this becomes *the* web package for other projects, add:

- `IHttpApi` (extract from the existing surface — mechanical, non-breaking),
- `ISseClient`,
- `IWebSocketConnectionFactory` / `IWebSocketConnection`.

This matches the solution's existing `…Abstraction` convention and makes every consumer trivially testable. (Interfaces can live in this package rather than the DB `Abstraction` project, since this package is independent of the relational stack.)

### 3.3 Extract a shared `ReconnectPolicy`

Both SSE and WebSockets need "retry the connection with capped exponential backoff + jitter." You already have exactly this math inside `RetryAfterDelegatingHandler.ComputeDelay`. Extract it into a reusable, `TimeProvider`-testable `ReconnectPolicy` (max attempts, base/max backoff, jitter) and have SSE and WS both consume it. Keeps behavior consistent and tested in one place.

---

### 3.4 Server-Sent Events

**Building block:** .NET 9+ ships `System.Net.ServerSentEvents.SseParser<T>`, which parses a `text/event-stream` into `IAsyncEnumerable<SseItem<T>>` (handles `data:`/`event:`/`id:`/`retry:` framing and multi-line data). Use it rather than hand-rolling the parser. (Available on `net11.0`; may need the `System.Net.ServerSentEvents` package reference.)

**Proposed surface:**

```csharp
public interface ISseClient
{
    // Raw events as they arrive.
    IAsyncEnumerable<SseItem<string>> SubscribeAsync(
        string url, SseSubscribeOptions? options = null, CancellationToken ct = default);

    // Typed: each event's data deserialized as T via this package's Serializer.
    IAsyncEnumerable<T> SubscribeAsync<T>(
        string url, SseSubscribeOptions? options = null, CancellationToken ct = default);
}

public sealed class SseSubscribeOptions
{
    public bool AutoReconnect { get; set; } = true;
    public ReconnectPolicy Reconnect { get; set; } = ReconnectPolicy.Default;
    public string? LastEventId { get; set; }            // seed resumption
    public Action<HttpRequestMessage>? ConfigureRequest { get; set; } // auth headers, etc.
    public string? EventTypeFilter { get; set; }        // only yield this `event:` name
}
```

**Behavior to implement:**
- GET with `Accept: text/event-stream`, `HttpCompletionOption.ResponseHeadersRead`, no per-call timeout (streams are long-lived — same rule your streaming methods already follow).
- Track the last `id:` seen; on reconnect send `Last-Event-ID`. Honor the stream's `retry:` hint as the reconnect delay floor.
- On transport drop, reconnect per `ReconnectPolicy` until success, `ct`, or max attempts.
- Reuse the typed `HttpClient` from `IHttpClientFactory`, so **the existing throttle/retry handlers apply for free** on the initial connect.
- Deserialize typed events through `Serializer` with the instance's `JsonSerializerOptions`.

**DI:** `services.AddDeepSigmaSse()` registering `ISseClient` over a named/typed `HttpClient`.

---

### 3.5 WebSockets

**Building block:** `System.Net.WebSockets.ClientWebSocket` (BCL). **Important caveat:** `ClientWebSocket` does **not** go through `IHttpClientFactory`/`DelegatingHandler`, so the existing throttle/retry handlers **do not** apply to the WS handshake. Reconnection/backoff must be provided by this layer (via the shared `ReconnectPolicy`), not inherited from the HTTP handlers. Worth stating plainly in the docs.

**Proposed surface — factory + connection (not a static method bag):**

```csharp
public interface IWebSocketConnectionFactory
{
    Task<IWebSocketConnection> ConnectAsync(
        Uri url, WebSocketConnectionOptions? options = null, CancellationToken ct = default);
}

public interface IWebSocketConnection : IAsyncDisposable
{
    WebSocketConnectionState State { get; }
    event Action<WebSocketConnectionState>? StateChanged;

    // Full-duplex.
    Task SendAsync<T>(T message, CancellationToken ct = default);           // JSON via Serializer
    Task SendTextAsync(string text, CancellationToken ct = default);
    Task SendBinaryAsync(ReadOnlyMemory<byte> bytes, CancellationToken ct = default);

    // Receive side as a stream; one of these, or expose both.
    IAsyncEnumerable<T> ReceiveAsync<T>(CancellationToken ct = default);     // typed JSON
    IAsyncEnumerable<WebSocketMessage> ReceiveAsync(CancellationToken ct = default); // raw frames

    Task CloseAsync(WebSocketCloseStatus status = WebSocketCloseStatus.NormalClosure,
                    string? description = null, CancellationToken ct = default);
}

public sealed class WebSocketConnectionOptions
{
    public bool AutoReconnect { get; set; } = true;
    public ReconnectPolicy Reconnect { get; set; } = ReconnectPolicy.Default;
    public TimeSpan KeepAliveInterval { get; set; } = TimeSpan.FromSeconds(30);
    public IReadOnlyList<string> SubProtocols { get; set; } = [];
    public Action<ClientWebSocketOptions>? ConfigureSocket { get; set; }    // headers, auth, TLS
    public Func<IWebSocketConnection, CancellationToken, Task>? OnReconnected { get; set; } // re-subscribe hook
}
```

**Behavior to implement:**
- Single background receive loop reassembling partial frames (`EndOfMessage`) into complete messages; deliver via a bounded `Channel<T>` so a slow consumer applies back-pressure instead of unbounded buffering.
- Keepalive via `ClientWebSocketOptions.KeepAliveInterval` (and/or app-level ping if the server needs it).
- Graceful close handshake (`CloseOutputAsync` + drain), and robust teardown in `DisposeAsync`.
- Auto-reconnect via `ReconnectPolicy`; on reconnect fire `OnReconnected` so the consumer can re-send subscription frames (essential for market-data style feeds — resubscribe to symbols after a drop).
- Typed send/receive through `Serializer`.
- Thread-safety: serialize sends (`ClientWebSocket.SendAsync` is not concurrent-safe) behind a `SemaphoreSlim`.

**DI:** `services.AddDeepSigmaWebSockets()` registering `IWebSocketConnectionFactory`.

---

### 3.6 C# 15 language features worth adopting

The project targets `net11.0`, so C# 15 is the default language version. Three features are a genuinely good fit for this package; the rest aren't worth reaching for here.

**Worth using:**

- **Union types — the headline fit.** SSE and WebSocket payloads are *naturally* "exactly one of a fixed set," and a received frame or an operation result is exactly what unions model. Use them for:
  - **Received WebSocket frames:** `union WebSocketMessage(TextMessage, BinaryMessage, CloseMessage);` — consumers `switch` with compile-time exhaustiveness instead of inspecting `WebSocketMessageType` enums and casting.
  - **Operation results instead of throwing** on expected failures (connect refused, non-2xx, parse error): `union SendResult(Sent, Failed);`, `union ConnectResult(Connected, ConnectFailed);`. This is the native replacement for OneOf-style result types — and `OneOf` already shows up transitively in this package's output, so unions let you drop that style entirely for new surfaces.
  - **SSE parse outcome:** `union SseEvent<T>(Parsed<T>, Malformed);` so a bad event doesn't tear down the stream.

  Caveats to respect: a `union` lowers to a `struct` and **boxes value-type cases**, so don't put `int`/`struct` payloads on a hot per-message path without the hand-written non-boxing pattern; and in exhaustive `switch`es prefer an explicit `null => …` arm over `_` so a newly added case still warns.

- **Closed hierarchies for connection state that carries data.** `WebSocketConnectionState` is richer than a flat enum once states carry payloads. Model it as a closed record hierarchy so consumer `switch`es stay exhaustive across the assembly boundary:
  ```csharp
  public closed record class ConnectionState;
  public sealed record class Connecting : ConnectionState;
  public sealed record class Open : ConnectionState;
  public sealed record class Reconnecting(int Attempt, TimeSpan NextDelay) : ConnectionState;
  public sealed record class Closed(WebSocketCloseStatus? Status, string? Reason) : ConnectionState;
  ```
  (If state never carries data, a plain `enum` is still the right, lighter choice — don't over-model.) Treat the subtype list as public API: adding a case is a breaking change for consumers' exhaustive switches, which is exactly the signal you want.

**Minor / situational:**

- **Collection expression arguments (`with(…)`)** — tidies header/option construction, e.g. `[with(StringComparer.OrdinalIgnoreCase), …]` for a case-insensitive set or `[with(capacity: n), .. items]`. Nice-to-have, not structural.
- **Labeled `break`/`continue`** — the WS receive/reassembly loop nested inside a reconnect loop is a candidate (`break reconnect;` on a normal close). Reach for it only if extracting a method would be clumsier.

**Skip here:** extension indexers, the numerics changes (Decimal32/64/128, generic math, etc.), and the preview memory-safety model — none intersect a web-comms client.

**One caution for a shipping package:** unions and `closed` are brand-new syntax (the SDK was still prerelease in the Oct 2026 docs). They're safe to use *internally* now; before putting union/`closed` types on the **public API** that other projects compile against, confirm you're on the GA `net11.0` SDK, since every consumer must build with a C# 15 compiler. Phase 1 (interfaces, `ReconnectPolicy`) doesn't depend on any of this.

---

## 4. Smaller cleanups worth folding in

1. **Rename/replace the placeholders.** `ServerSideEventAPI` → `SseClient` (public, documented); `WebSocketApi` → the connection/factory types above. Drop the mis-cased `API`.
2. **Make the Alpha-Vantage rate-limit check pluggable.** `Serializer.LoadFromJson` hard-throws on `Note` / `Error Message` root properties — provider-specific logic baked into a general deserializer. Consider making it opt-in (a `ResponseGuard`/predicate) so the package isn't surprising for other APIs. Non-urgent, but relevant now that this is a general web package.
3. **`<Version>` format:** `2.0.0.0` → `2.0.0`.
4. **Unified DI entry point:** optionally `AddDeepSigmaWeb(opts => { opts.AddHttp(); opts.AddSse(); opts.AddWebSockets(); })` so consumers get everything with one call, while the granular registrations remain.

---

## 5. Suggested sequencing

1. ✅ **Foundation (done):** package renamed to `DeepSigma.DataAccess.Web` (id/assembly/namespaces, v3.0.0); `IHttpApi` extracted and registered in DI; `ReconnectPolicy` extracted into `…Web.Resilience` and consumed by `RetryAfterDelegatingHandler` (behavior unchanged); stale `HttpApi.LoadFrom*` tests/README realigned to `Serializer.*`; all 62 tests green. **Deferred:** physical folder / `.csproj` filename / `.sln` entry still say `…Http` — rename those in a deliberate pass (can disrupt an open VS solution).
2. ✅ **SSE (done):** `ISseClient` + `SseClient` over `System.Net.ServerSentEvents.SseParser` (inbox on net11, no package ref), typed + raw `IAsyncEnumerable`, auto-reconnect with `Last-Event-ID` resumption and `retry:`-hint-aware backoff via the shared `ReconnectPolicy`, event-type filter, `AddDeepSigmaSse()` DI; 6 tests (stub `text/event-stream` + `FakeTimeProvider`), README section. Old `ServerSideEventApi` placeholder removed.
3. ✅ **WebSockets (done):** `IWebSocketConnectionFactory` → `IWebSocketConnection` over `ClientWebSocket`; single receive loop reassembling frames into a bounded `Channel`; typed + raw receive where the message type is a **C# 15 `union WebSocketMessage(Text, Binary, Close)`**; serialized sends; keep-alive; auto-reconnect via the shared `ReconnectPolicy` with an `OnReconnected` re-subscribe hook; `AddDeepSigmaWebSockets()` DI. Testable via an injectable socket connector seam (`InternalsVisibleTo`) + an in-memory `FakeWebSocket`; 7 tests. Old `WebSocketApi` placeholder removed. State is a plain enum for now (could become a `closed` record hierarchy if states need payloads).
4. ✅ **Polish (done):** unified `AddDeepSigmaWeb(w => …)` builder (all three transports or a subset); the `Note`/`Error Message` check is now a pluggable, **opt-in** `JsonResponseGuard` (`JsonResponseGuards.AlphaVantage`) on `Serializer.LoadFromJson` and the `HttpApi` constructor — no longer applied by default; also hardened the guard against non-object roots. README + changelog updated; 81 tests green. **Remaining before tagging `3.0.0`:** the deferred physical folder / `.csproj` filename / `.sln` rename (cosmetic-internal), then publish + deprecate the old `…Http` package id.

Each phase is independently shippable and keeps existing consumers compiling.

---

## 6. Open decisions for you

- ✅ **§3.1 — Package identity:** **decided — rename to `DeepSigma.DataAccess.Web`.**
- **§3.2 — Interfaces:** introduce `IHttpApi`/`ISseClient`/`IWebSocket…` now (recommended), or keep concrete classes?
- **§3.6 — C# 15 unions on the public API:** adopt union/`closed` types on the public surface now, or use them internally first and keep the public API on plain enums + exceptions until the SDK is GA (recommended)?
- **WS delivery model:** `IAsyncEnumerable<T>` only, raw-frames only, or both (recommended)?
- **SSE parser dependency:** rely on `System.Net.ServerSentEvents` (recommended) vs. a hand-rolled parser with zero extra dependency?
