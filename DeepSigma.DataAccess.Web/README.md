# DeepSigma.DataAccess.Web

Web-communication clients for DeepSigma applications, built on `HttpClient`. Today:

1. **`HttpApi`** (behind **`IHttpApi`**) — typed-HTTP helper for fetching and deserializing **JSON**, **CSV**, and **XML** payloads; sending **`POST`/`PUT`/`PATCH`/`DELETE`** with JSON or form bodies; streaming binary downloads; and streaming large XML element-by-element. A `SendAsync` escape hatch covers anything the typed helpers don't.
2. **`ISseClient`** — Server-Sent Events (`text/event-stream`) consumed as an `IAsyncEnumerable`, with transparent reconnection and `Last-Event-ID` resumption.
3. **`IWebSocketConnectionFactory` / `IWebSocketConnection`** — full-duplex WebSockets with typed/raw send and a reassembled receive stream, keep-alive, and auto-reconnect with a re-subscribe hook.
4. **Reusable `DelegatingHandler`s** — `MinIntervalDelegatingHandler` (throttling) and `RetryAfterDelegatingHandler` (server-driven backoff), composable on any named or typed `HttpClient` via `IHttpClientBuilder` extensions, plus a shared `ReconnectPolicy` (capped exponential backoff with jitter) in `DeepSigma.DataAccess.Web.Resilience`.

This package is the home for all three transports — HTTP, SSE, and WebSockets.

> **Renamed in 3.0.0:** this package was `DeepSigma.DataAccess.Http`. The package id, assembly, and root namespace are now `DeepSigma.DataAccess.Web`. Update `using DeepSigma.DataAccess.Http;` → `using DeepSigma.DataAccess.Web;` (and `.Http.Throttling` → `.Web.Throttling`).

This package is **independent** of the relational stack and intended for consumption by API-specific data-access libraries (e.g. `DeepSigma.DataAccess.ArXiv`, `DeepSigma.AlphaVantage`).

## Installation

```bash
dotnet add package DeepSigma.DataAccess.Web
```

## Dependencies

| Package | Purpose |
|---|---|
| `DeepSigma.Core` ≥ 1.3.0 | XML helpers (`XMLUtilities`, `XmlReaderExtensions`) used by the XML fetch methods. |
| `DeepSigma.DataAccess.CsvUtilities` | CSV parsing used by `LoadFromCsv` / `GetDataFromCsvAsync`. |
| `Microsoft.Extensions.Http` | `IHttpClientFactory` integration for the DI extensions. |

Uses `System.Net.Http.HttpClient` and `System.Text.Json` from the BCL — no extra packages needed for those.

## What it provides

An instance class `HttpApi` with helpers for JSON, CSV, XML, raw-string, streaming-XML, and binary downloads, write verbs (`POST`/`PUT`/`PATCH`/`DELETE`), and a full-control `SendAsync` escape hatch — plus pure static deserializers that don't need an `HttpClient`. On top of that, two reusable `DelegatingHandler`s and `IHttpClientBuilder` extensions to compose them.

### Read methods — `GET` (require an `HttpClient` via constructor)

| Method | Behaviour |
|---|---|
| `GetDataFromUrlAsync<T>(url, timeoutInSeconds = 15, apiResultLoggingMethod = null, cancellationToken = default)` | `GET`s the URL, deserialises the JSON body into `T`. Optionally hands the raw body to a logging callback first. |
| `GetDataFromCsvAsync<T>(url, timeoutInSeconds = 15, apiResultLoggingMethod = null, cancellationToken = default)` | `GET`s the URL, parses the CSV body into `List<T>` via `DeepSigma.DataAccess.CsvUtilities`. |
| `GetDataFromXmlUrlAsync<T>(url, timeoutInSeconds = 15, apiResultLoggingMethod = null, cancellationToken = default)` | `GET`s the URL, deserialises the XML body into `T` via `XmlSerializer`. |
| `GetJsonResponseAsync(url, timeoutInSeconds = 15, cancellationToken = default)` | `GET`s the URL and returns the raw body as a string. Throws on non-success status. |
| `GetXmlResponseAsync(url, timeoutInSeconds = 15, cancellationToken = default)` | `GET`s the URL and returns the raw body as a string. |
| `GetCsvDataAsync(url, timeoutSeconds = 15, cancellationToken = default)` | Same as `GetJsonResponseAsync`, but additionally validates that the `Content-Type` looks like a CSV. Throws if it does not. |
| `StreamXmlElementsAsync<T>(url, elementName, namespaceUri = null, cancellationToken = default)` | `GET`s the URL and streams matching XML elements as `IAsyncEnumerable<T>`. Memory stays bounded regardless of response size. |
| `DownloadToStreamAsync(url, destination, bufferSize = 81_920, cancellationToken = default)` | `GET`s the URL and copies the response body into `destination`. Suitable for binary downloads or any large payload. |
| `DownloadToFileAsync(url, destinationPath, cancellationToken = default)` | Convenience wrapper that writes to a file path, creating the destination's directory if needed. |

### Write methods — `POST`/`PUT`/`PATCH`/`DELETE`

All share the read methods' semantics: per-call timeout via linked `CancellationTokenSource`, `EnsureSuccessStatusCode`, and an optional `apiResultLoggingMethod` that receives the raw response body before deserialisation. Outgoing JSON bodies are serialised with the instance's configured `JsonSerializerOptions` (see [JSON serialization options](#json-serialization-options)).

| Method | Behaviour |
|---|---|
| `PostJsonAsync<TRequest, TResponse>(url, body, timeoutInSeconds = 15, apiResultLoggingMethod = null, cancellationToken = default)` | Serialises `body` as JSON, `POST`s it, deserialises the JSON response into `TResponse`. |
| `PostJsonAsync<TRequest>(url, body, timeoutInSeconds = 15, cancellationToken = default)` | Same, but returns the raw response body as a string instead of deserialising. |
| `PostFormAsync<TResponse>(url, fields, timeoutInSeconds = 15, apiResultLoggingMethod = null, cancellationToken = default)` | `POST`s `fields` as `application/x-www-form-urlencoded` (common for OAuth token endpoints), deserialises the JSON response into `TResponse`. |
| `PutJsonAsync<TRequest, TResponse>(url, body, timeoutInSeconds = 15, apiResultLoggingMethod = null, cancellationToken = default)` | `PUT` analogue of `PostJsonAsync<TRequest, TResponse>`. |
| `PatchJsonAsync<TRequest, TResponse>(url, body, timeoutInSeconds = 15, apiResultLoggingMethod = null, cancellationToken = default)` | `PATCH` analogue of `PostJsonAsync<TRequest, TResponse>`. |
| `DeleteAsync(url, timeoutInSeconds = 15, apiResultLoggingMethod = null, cancellationToken = default)` | `DELETE`s the URL; returns the raw response body string (often empty). Throws on non-success status. |
| `PostJsonToStreamAsync<TRequest>(url, body, destination, bufferSize = 81_920, cancellationToken = default)` | `POST`s a JSON body and streams the response into `destination` without buffering. For binary or large responses. |
| `PutJsonToStreamAsync<TRequest>(url, body, destination, bufferSize = 81_920, cancellationToken = default)` | `PUT` analogue of `PostJsonToStreamAsync`. |

### Escape hatch — full request control

| Method | Behaviour |
|---|---|
| `SendAsync(request, timeoutInSeconds = 15, validator = null, cancellationToken = default)` | Sends a caller-built `HttpRequestMessage` (custom headers, multipart, non-JSON bodies), applies the per-call timeout and status check, runs the optional `validator`, and returns the response body string. The request is disposed. |
| `SendToStreamAsync(request, destination, bufferSize = 81_920, cancellationToken = default)` | Same, but streams the response body into `destination`. |
| `SendForResponseAsync(request, timeoutInSeconds = 15, cancellationToken = default)` | Sends a caller-built request and returns an `HttpApiResponse` whatever the status: `StatusCode`, `Body`, `Headers` (response and content, any case), `RetryAfter` and `IsSuccess`. Never throws for a status, so a refusal's reason in its body reaches the caller. The request is disposed. |

### Static helpers (pure — no `HttpClient` needed)

| Method | Behaviour |
|---|---|
| `Serializer.LoadFromJson<T>(jsonText, options = null, guard = null)` | Deserialises JSON text into `T`. Pass a `JsonResponseGuard` (a vendor package's own, such as the AlphaVantage package's) to turn HTTP-200 rate-limit / error payloads into exceptions; with no guard, none runs. Uses `DefaultJsonOptions` when `options` is omitted. |
| `Serializer.LoadFromCsv<T>(csvText)` | Parses CSV text into `List<T>?`. Returns `null` if parsing fails. |
| `Serializer.LoadFromXml<T>(xmlText)` | Deserialises XML text into `T` via `XmlSerializer`. Returns default for empty input. |

## JSON serialization options

Each `HttpApi` instance carries one `JsonSerializerOptions` used for **both** outgoing request bodies and incoming response deserialisation. Two presets ship as static properties, and you can supply your own:

| Preset | Behaviour |
|---|---|
| `HttpApi.DefaultJsonOptions` | `NumberHandling = AllowReadingFromString` (accepts numbers quoted as strings) and `PropertyNameCaseInsensitive = true` (matches camelCase JSON onto PascalCase properties on read). Property names go on the wire **as-is** — PascalCase unless you apply `[JsonPropertyName]`. This is the default when you don't pass options. |
| `HttpApi.SnakeCaseJsonOptions` | The same, plus `PropertyNamingPolicy` / `DictionaryKeyPolicy = SnakeCaseLower` — PascalCase C# properties are written as `snake_case` on the wire and read case-insensitively. Use for Python / FastAPI-style APIs. |

Select per instance via the constructor's third argument:

```csharp
// Default options:
var http = new HttpApi(new HttpClient());

// snake_case API:
var snake = new HttpApi(new HttpClient(), jsonOptions: HttpApi.SnakeCaseJsonOptions);

// Fully custom (e.g. camelCase request bodies):
var camel = new HttpApi(new HttpClient(), jsonOptions: new JsonSerializerOptions
{
    PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    PropertyNameCaseInsensitive = true,
});
```

The static `LoadFromJson<T>` helper takes the same options as an optional argument and falls back to `DefaultJsonOptions`. For one-off requests that need a different shape than the instance default, build the request yourself and send it via `SendAsync`.

> **DI note:** `AddDeepSigmaHttp()` activates `HttpApi` through `IHttpClientFactory`, which uses `DefaultJsonOptions`. To register an instance with a different preset, add your own typed-client registration that passes `jsonOptions` to the constructor.

## Dependency-injection registration

```csharp
using Microsoft.Extensions.DependencyInjection;

// Default: HttpApi registered as a typed client; HttpClient managed by IHttpClientFactory
services.AddDeepSigmaHttp();

// Or: customize the underlying HttpClient (default headers, base address, etc.)
services.AddDeepSigmaHttp(http =>
{
    http.BaseAddress = new Uri("https://api.example.com/");
    http.DefaultRequestHeaders.Add("X-Api-Key", "your-key");
});
```

Granular registrations exist per transport — `AddDeepSigmaHttp()`, `AddDeepSigmaSse()`, `AddDeepSigmaWebSockets()` — or register them together with the unified entry point:

```csharp
// All three transports with defaults:
services.AddDeepSigmaWeb();

// Or select and configure a subset:
services.AddDeepSigmaWeb(w => w
    .AddHttp(c => c.BaseAddress = new Uri("https://api.example.com/"))
    .AddSse());
```

Then inject `HttpApi` (or `IHttpApi`) into your services:

```csharp
public class WeatherService(HttpApi http)
{
    public Task<WeatherResponse?> GetForCity(string city, CancellationToken ct)
        => http.GetDataFromUrlAsync<WeatherResponse>($"weather?city={city}", cancellationToken: ct);
}
```

## Quick start: JSON

```csharp
using DeepSigma.DataAccess.Web;

public sealed class WeatherResponse
{
    public string City { get; set; } = "";
    public double TempF { get; set; }
}

// In a real app, get HttpApi via DI. For ad-hoc use, you can construct directly:
var http = new HttpApi(new HttpClient());

WeatherResponse? weather = await http.GetDataFromUrlAsync<WeatherResponse>(
    "https://example.com/api/weather?city=Seattle");
```

## Quick start: CSV

```csharp
using DeepSigma.DataAccess.Web;

public sealed class Trade
{
    public DateTime Date { get; set; }
    public string Symbol { get; set; } = "";
    public decimal Price { get; set; }
}

List<Trade> trades = await http.GetDataFromCsvAsync<Trade>(
    "https://example.com/data/trades.csv");
```

## Quick start: separate fetch + parse

If you want to log, cache, or transform the raw body before parsing:

```csharp
string? raw = await http.GetJsonResponseAsync(url);
File.WriteAllText("snapshot.json", raw);          // cache
WeatherResponse? dto = Serializer.LoadFromJson<WeatherResponse>(raw!);
```

## Quick start: XML

```csharp
using System.Xml.Serialization;
using DeepSigma.DataAccess.Web;

[XmlRoot("rss")]
public sealed class RssFeed
{
    [XmlElement("channel")]
    public RssChannel? Channel { get; set; }
}

RssFeed? feed = await http.GetDataFromXmlUrlAsync<RssFeed>("https://example.com/feed.xml");
```

Or fetch and project by hand for hand-rolled XML payloads:

```csharp
using System.Xml.Linq;

string? xml = await http.GetXmlResponseAsync("https://example.com/feed.xml");
XDocument doc = XDocument.Parse(xml!);
var titles = doc.Descendants("item").Select(i => i.Element("title")?.Value);
```

## Quick start: streaming large XML

When the response is too big to materialize as `XDocument` (OAI-PMH harvest pages, RSS dumps, S3 inventory) — yield matching elements one at a time without buffering the document:

```csharp
[XmlRoot("record")]
public sealed class HarvestRecord
{
    [XmlElement("id")]    public string Id { get; set; } = "";
    [XmlElement("title")] public string Title { get; set; } = "";
}

await foreach (var record in http.StreamXmlElementsAsync<HarvestRecord>(
                   "https://huge-feed.example.com/list",
                   elementName: "record",
                   cancellationToken: ct))
{
    Console.WriteLine($"{record.Id}: {record.Title}");
}
```

Memory usage stays O(1) regardless of response size. Pair with `MinIntervalDelegatingHandler` and `RetryAfterDelegatingHandler` for a complete polite-harvester setup (see below).

## Quick start: streaming binary downloads

```csharp
await using var file = File.Create("./out/payload.bin");
await http.DownloadToStreamAsync("https://example.com/large.zip", file, cancellationToken: ct);

// Or the convenience wrapper that auto-creates parent directories:
await http.DownloadToFileAsync("https://example.com/large.zip", "./out/payload.bin", ct);
```

Both stream the response body in 80 KB chunks (configurable) and never buffer the whole payload in memory.

## Quick start: POST / PUT / PATCH / DELETE

The write verbs mirror the read helpers — same per-call timeout, status-check, and optional logging callback — and serialise the request body as JSON for you:

```csharp
public sealed record CreateUserRequest(string Name, int Age);
public sealed record UserResponse(string Id, string Name);

// POST a JSON body, get the deserialized response back:
UserResponse? created = await http.PostJsonAsync<CreateUserRequest, UserResponse>(
    "https://api.example.com/users",
    new CreateUserRequest("Ada", 37));

// PUT / PATCH follow the same shape:
await http.PutJsonAsync<CreateUserRequest, UserResponse>($"users/{id}", updated);
await http.PatchJsonAsync<CreateUserRequest, UserResponse>($"users/{id}", patch);

// DELETE returns the raw response body (often empty); throws on non-2xx:
string? body = await http.DeleteAsync($"users/{id}");
```

Form-encoded bodies (the usual shape for OAuth token endpoints):

```csharp
public sealed record TokenResponse(string Access_Token, int Expires_In);

TokenResponse? token = await http.PostFormAsync<TokenResponse>(
    "https://auth.example.com/oauth/token",
    new[]
    {
        new KeyValuePair<string, string>("grant_type", "client_credentials"),
        new KeyValuePair<string, string>("client_id", clientId),
        new KeyValuePair<string, string>("client_secret", clientSecret),
    });
```

Capture the raw response upstream for logging or caching — every typed write method accepts `apiResultLoggingMethod`, invoked with the raw body *before* deserialisation (the same hook the `GET` helpers expose):

```csharp
UserResponse? created = await http.PostJsonAsync<CreateUserRequest, UserResponse>(
    "users", request,
    apiResultLoggingMethod: raw => _logger.LogDebug("POST /users -> {Body}", raw));
```

## Quick start: full request control (`SendAsync`)

When the typed helpers don't fit — custom per-request headers, multipart uploads, non-JSON bodies, or an unusual verb — build the `HttpRequestMessage` yourself and hand it to `SendAsync`. You still get the per-call timeout, status enforcement, and an optional response validator:

```csharp
using var request = new HttpRequestMessage(HttpMethod.Post, "https://api.example.com/upload")
{
    Content = new MultipartFormDataContent
    {
        { new ByteArrayContent(bytes), "file", "report.pdf" },
    },
};
request.Headers.Add("X-Idempotency-Key", idempotencyKey);

string? body = await http.SendAsync(
    request,
    validator: (response, raw) =>
    {
        if (response.Headers.Location is null)
            throw new InvalidOperationException($"Expected a Location header: {raw}");
    });
```

`SendToStreamAsync(request, destination)` is the streaming counterpart for large or binary responses. Both dispose the request for you.

For an API that explains a refusal in its body (`400 Invalid key 'x'.`) or whose caller decides what each status means,
`SendForResponseAsync` returns the response whatever its status:

```csharp
var answer = await http.SendForResponseAsync(new HttpRequestMessage(HttpMethod.Post, "v3/mapping") { Content = json });
if (!answer.IsSuccess)
    throw new MyApiException((int)answer.StatusCode, answer.Body, answer.RetryAfter);   // the body says why
```

---

## Server-Sent Events (SSE)

`ISseClient` / `SseClient` consume a `text/event-stream` as an `IAsyncEnumerable`, parsing the wire format with the BCL's `System.Net.ServerSentEvents.SseParser`. It adds transparent reconnection on top: when the stream ends or the transport drops, it reconnects (sending `Last-Event-ID` to resume) and spaces attempts with the shared `ReconnectPolicy`, honoring the server's `retry:` hint.

```csharp
using Microsoft.Extensions.DependencyInjection;

services.AddDeepSigmaSse(c => c.BaseAddress = new Uri("https://api.example.com/"));
// The throttle/retry handlers compose here too:
services.AddHttpClient<SseClient>().AddRetryAfterPolicy();
```

```csharp
// Typed: each event's JSON data deserialized as T (uses the client's JsonSerializerOptions).
await foreach (PriceTick tick in sse.SubscribeAsync<PriceTick>("prices/stream", cancellationToken: ct))
{
    Console.WriteLine($"{tick.Symbol} {tick.Price}");
}

// Raw: the full SseItem<string> (data, event type, id, reconnect interval).
await foreach (var evt in sse.SubscribeAsync("prices/stream", cancellationToken: ct))
{
    Console.WriteLine($"[{evt.EventType}] {evt.Data}");
}
```

Tune a subscription with `SseSubscribeOptions`:

| Option | Behaviour |
|---|---|
| `AutoReconnect` (default `true`) | Reconnect after stream end/fault, resuming from the last event id. `false` completes enumeration when the stream ends. |
| `Reconnect` | The `ReconnectPolicy` (capped exponential backoff + jitter) spacing attempts. Set `MaxAttempts = int.MaxValue` to reconnect indefinitely. |
| `LastEventId` | Seed the `Last-Event-ID` header on first connect to resume a prior stream. |
| `EventTypeFilter` | Yield only events with this `event:` type; others still advance the resume point. |
| `ConfigureRequest` | Mutate each outgoing request (auth headers, query) — invoked on connect and every reconnect. |

The subscription is long-lived and has **no per-call timeout**; its lifetime is bounded by your `CancellationToken`. The connect phase is still bounded by the underlying `HttpClient.Timeout` (the request uses `ResponseHeadersRead`). Inject a `TimeProvider` into `SseClient` to drive reconnection delays deterministically in tests.

---

## WebSockets

`IWebSocketConnectionFactory` opens `IWebSocketConnection`s over `ClientWebSocket`. A single background loop reads and reassembles frames into whole messages and publishes them to a bounded channel (back-pressure to the socket when the consumer lags); sends are serialized for you. On a transport fault it reconnects per the shared `ReconnectPolicy` and runs your re-subscribe hook.

```csharp
using Microsoft.Extensions.DependencyInjection;

services.AddDeepSigmaWebSockets();               // registers IWebSocketConnectionFactory
```

```csharp
var options = new WebSocketConnectionOptions
{
    AutoReconnect = true,
    OnReconnected = async (conn, ct) => await conn.SendAsync(new Subscribe("AAPL"), ct), // re-subscribe after a drop
};

await using IWebSocketConnection conn = await factory.ConnectAsync(new Uri("wss://feed.example.com/ws"), options, ct);

await conn.SendAsync(new Subscribe("AAPL"), ct);   // typed -> JSON text frame

await foreach (Quote quote in conn.ReceiveAsync<Quote>(ct))   // typed receive
{
    Console.WriteLine($"{quote.Symbol} {quote.Price}");
}
```

Received messages are a **C# 15 union** — switch over the three cases exhaustively when you want the raw stream:

```csharp
await foreach (WebSocketMessage message in conn.ReceiveAsync(ct))
{
    switch (message)
    {
        case WebSocketTextMessage t:   Handle(t.Text); break;
        case WebSocketBinaryMessage b: Handle(b.Bytes); break;
        case WebSocketCloseMessage c:  Console.WriteLine($"closed: {c.Status}"); break;
    }
}
```

| Option | Behaviour |
|---|---|
| `AutoReconnect` (default `true`) | Reconnect after a transport fault and run `OnReconnected`. A clean close never reconnects. |
| `Reconnect` | The `ReconnectPolicy` spacing reconnect attempts. |
| `KeepAliveInterval` | Ping interval on the underlying `ClientWebSocket` (default 30s). |
| `SubProtocols` / `ConfigureSocket` | Handshake sub-protocols and raw `ClientWebSocketOptions` access (headers, auth, TLS). |
| `OnReconnected` | Invoked after a successful reconnect, before delivery resumes — re-send your subscription frames here. |
| `ReceiveQueueCapacity` / `ReceiveBufferSize` | Bounded receive-queue depth and per-read buffer size. |

A connection is a single-consumer receive stream; sends are serialized internally and safe to call concurrently. Dispose the connection (`await using`) to close and release the socket. Inject a `TimeProvider` into `WebSocketConnectionFactory` to drive reconnection delays deterministically in tests.

---

## Rate limiting and retry: `DelegatingHandler`s

Two small handlers ship alongside `HttpApi`. They're generic, hand-rolled (no Polly dep), and composable with `IHttpClientBuilder` chaining.

### `MinIntervalDelegatingHandler`

Enforces a minimum delay between outgoing requests sharing a string key. Process-scoped semaphore — multiple `HttpClient` instances using the same key cooperate on spacing.

```csharp
using DeepSigma.DataAccess.Web.Throttling;

services.AddHttpClient("polite-api")
        .AddMinIntervalThrottle(
            minInterval: TimeSpan.FromSeconds(3),
            key:         "example.com");   // shared throttle key
```

**Key isolation.** Two handlers with the same key share state; different keys don't interfere. If `key` is omitted, the client's registered name is used. This is the right pattern when you have multiple `HttpClient` instances all hitting one API and want them to cooperate (e.g. multiple consumers of a typed-client wrapping the same backend).

**Process-scoped caveat.** The semaphore lives in a `ConcurrentDictionary<string, ThrottleState>` in process memory. Two processes on the same host won't coordinate. Most APIs measure rate per-IP, so don't spawn parallel processes that talk to the same backend.

**Testability.** The constructor accepts an optional `TimeProvider` (defaults to `TimeProvider.System`). Inject `Microsoft.Extensions.Time.Testing.FakeTimeProvider` in tests to advance the clock deterministically without sleeping.

### `RetryAfterDelegatingHandler`

Retries on configured status codes (default: 429, 503), honoring the `Retry-After` header (delta-seconds or HTTP-date). Falls back to capped exponential backoff with jitter when the header is missing.

```csharp
using DeepSigma.DataAccess.Web.Throttling;

services.AddHttpClient("retrying-api")
        .AddRetryAfterPolicy(o =>
        {
            o.MaxAttempts      = 5;                                 // including the initial attempt
            o.BaseBackoff      = TimeSpan.FromSeconds(2);           // first backoff
            o.MaxBackoff       = TimeSpan.FromMinutes(10);          // cap on any single wait
            o.RetryStatusCodes = new() { (HttpStatusCode)429, HttpStatusCode.ServiceUnavailable };
        });
```

Defaults match what's shown above. Override `RetryStatusCodes` to add (e.g.) `BadGateway`, or to remove `429` if you handle it elsewhere.

`RetryAfterHeader.GetDelay(response, clock)` is the handler's own reading of the header, public for a client that
reports the wait to its caller rather than waiting.

When `MaxAttempts` is exhausted, the *final* response (with whatever failure status) is returned to the caller — it's not converted into an exception. The caller's `EnsureSuccessStatusCode()` (or framework like `HttpApi.GetJsonResponseAsync`) decides whether to throw.

### Composition

The handlers stack. On `IHttpClientBuilder` the first handler added is the outermost, so add the retry first: it then
resubmits through the throttle, and every attempt is paced, retries included.

```csharp
services.AddHttpClient("polite-and-retrying")
        .ConfigureHttpClient(c => c.BaseAddress = new Uri("https://api.example.com/"))
        .AddRetryAfterPolicy(o => o.MaxAttempts = 5)
        .AddMinIntervalThrottle(TimeSpan.FromSeconds(3), key: "example.com");
```

Added the other way round (throttle, then retry), the retry sits inside the throttle and its attempts are not paced.

### Without dependency injection: `HttpPipeline`

A library that makes its own `HttpClient` (no service container) composes the same handlers, in the same order:

```csharp
using DeepSigma.DataAccess.Web.Throttling;

var client = HttpPipeline.CreateClient(new SocketsHttpHandler(), new HttpPipelineOptions
{
    Retry       = new RetryAfterOptions { MaxAttempts = 5 },
    Throttle    = new MinIntervalOptions { Key = "example.com", MinInterval = TimeSpan.FromSeconds(3) },
    BaseAddress = new Uri("https://api.example.com/"),
    Timeout     = Timeout.InfiniteTimeSpan,          // when the caller times each call itself
});
```

Pass `disposeHandler: false` when the handler is shared by several clients or belongs to the caller: disposing the
client then leaves it open (`NonDisposingHandler`). `HttpPipeline.CreateHandler` returns the chain alone. `Clock` takes a
`FakeTimeProvider` in tests.

---

## End-to-end example: a polite typed client

Putting it all together — a typed client that hits `https://api.example.com/`, paces at one request per second, retries 429/503 with `Retry-After`, and deserializes JSON:

```csharp
using DeepSigma.DataAccess.Web;
using DeepSigma.DataAccess.Web.Throttling;
using Microsoft.Extensions.DependencyInjection;

public sealed record ExampleDto(string Id, string Name);

public sealed class ExampleClient(HttpClient http)
{
    public Task<ExampleDto?> GetAsync(string id, CancellationToken ct = default)
    {
        var api = new HttpApi(http);
        return api.GetDataFromUrlAsync<ExampleDto>($"items/{id}", cancellationToken: ct);
    }
}

// DI wire-up:
services.AddHttpClient<ExampleClient>(c =>
        {
            c.BaseAddress = new Uri("https://api.example.com/");
            c.DefaultRequestHeaders.UserAgent.ParseAdd("MyApp/1.0 (contact@example.com)");
        })
        .AddRetryAfterPolicy(o => o.MaxAttempts = 5)                        // first: the outermost, so retries are paced
        .AddMinIntervalThrottle(TimeSpan.FromSeconds(1), key: "example.com");
```

Consumers inject `ExampleClient`; the throttling and retry behaviors are entirely transparent.

---

## Testability

### `StubHttpMessageHandler` pattern

For unit-testing code that consumes `HttpApi` or your typed clients, use a stub message handler (an example lives in this repo's test project at `Infrastructure/StubHttpMessageHandler.cs`):

```csharp
internal sealed class StubHttpMessageHandler : HttpMessageHandler
{
    public Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>>? Responder { get; set; }
    public List<HttpRequestMessage> Requests { get; } = new();

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
    {
        Requests.Add(request);
        return Responder!(request, ct);
    }
}
```

Then construct `HttpApi` against an `HttpClient` wrapping it — no network involved.

### `FakeTimeProvider` for handler tests

`MinIntervalDelegatingHandler` and `RetryAfterDelegatingHandler` accept an optional `TimeProvider`. Pair with `Microsoft.Extensions.TimeProvider.Testing.FakeTimeProvider` to test timing-sensitive behavior deterministically:

```csharp
using Microsoft.Extensions.Time.Testing;

var clock   = new FakeTimeProvider(startDateTime: DateTimeOffset.UtcNow);
var handler = new MinIntervalDelegatingHandler(
    new MinIntervalOptions { MinInterval = TimeSpan.FromSeconds(3), Key = "test" },
    clock)
{ InnerHandler = new StubHttpMessageHandler { /* ... */ } };

var client = new HttpClient(handler);

await client.GetAsync("https://example.com/a");   // first request — no wait
var task = client.GetAsync("https://example.com/b");  // parks on throttle

Assert.False(task.IsCompleted);
clock.Advance(TimeSpan.FromSeconds(3));            // release the delay
await task;                                         // now completes
```

Tests run in milliseconds — no real waiting.

---

## Notes

- **Rate-limit detection (opt-in).** Supply a `JsonResponseGuard` — either to `Serializer.LoadFromJson` or to the `HttpApi` constructor (`responseGuard:`) — to inspect the response root and throw on API-level errors. `JsonResponseGuards.AlphaVantage` throws `InvalidOperationException` when the root carries a `Note` or `Error Message` property (the HTTP-200 throttle convention used by Alpha Vantage and similar providers). **Changed in 3.0.0:** this check is no longer applied by default — opt in with a guard, or write your own `JsonResponseGuard` for your provider's convention.
- **HttpClient lifetime.** When you register via `AddDeepSigmaHttp()`, `IHttpClientFactory` manages the underlying `HttpClient` (handler pooling, DNS rotation, etc.). When you construct `HttpApi` directly, you control the `HttpClient` lifetime yourself — share a long-lived instance across your application rather than constructing one per call.
- **Per-call timeouts.** Enforced via a linked `CancellationTokenSource` independently of the injected `HttpClient.Timeout`. The factory-managed client's own timeout is ignored. The buffered methods (reads and write verbs) apply this timeout; the streaming methods (`StreamXmlElementsAsync`, `DownloadToStreamAsync`, `*ToStreamAsync`) do not — their duration is caller-bounded via the `CancellationToken`, since a long stream legitimately outlives a 15-second request timeout.
- **Write-body serialisation.** `PostJsonAsync` / `PutJsonAsync` / `PatchJsonAsync` serialise the request body with the instance's `JsonSerializerOptions`. Pick `DefaultJsonOptions` (PascalCase) or `SnakeCaseJsonOptions` via the constructor, or pass a custom instance — see [JSON serialization options](#json-serialization-options).
- **Request disposal.** `SendAsync` and `SendToStreamAsync` dispose the `HttpRequestMessage` (and its `Content`) you hand them. Don't reuse a request instance across calls — build a fresh one each time, as the typed helpers do internally.
- **Cancellation.** Every instance method accepts an optional `CancellationToken` and links it with the timeout-driven CTS so either trigger will cancel the request.
- **CSV content-type check.** `GetCsvDataAsync` validates that the response `Content-Type` contains the substring `"csv"`. If the server returns the data with a generic `text/plain` or `application/octet-stream`, the call will throw — use `GetJsonResponseAsync` + `LoadFromCsv` to bypass the check.
- **XML deserialization** uses `System.Xml.Serialization.XmlSerializer` via `DeepSigma.Core.Utilities.XMLUtilities`. Decorate your types with `[XmlRoot]`, `[XmlElement]`, `[XmlAttribute]` as needed.
- **`StreamXmlElementsAsync` namespace filtering** is opt-in: pass `namespaceUri` to require a specific XML namespace, or leave null to match by local name only.
- **`DownloadToStreamAsync` honors cancellation** between buffer copies. A cancellation during a 1 GB download interrupts the next 80 KB chunk read — typically well under a second of delay.
- **Throttle keys are case-sensitive.** `"example.com"` and `"Example.com"` are different throttles.

## Changelog

| Version | Notes |
|---|---|
| `4.0.0` | **`HttpPipeline`** composes the retry and spacing handlers without dependency injection (`HttpPipelineOptions`: retry, throttle, clock, base address, timeout), with `NonDisposingHandler` for a handler the client does not own. **`HttpApi.SendForResponseAsync`** returns an `HttpApiResponse` (status, body, headers, `RetryAfter`) whatever the status, for APIs whose refusals carry their reason in the body; the constructor takes an optional `TimeProvider`. **`RetryAfterHeader.GetDelay`** is the retry handler's reading of `Retry-After`, now public. **Documentation fix:** on `IHttpClientBuilder` the first handler added is the outermost, so add the retry before the throttle to pace retries (the examples had it the other way round). **Breaking:** `JsonResponseGuards.AlphaVantage` is removed — it is AlphaVantage's convention and lives in the AlphaVantage package (`AlphaVantageResponseGuard`); `IHttpApi` gains `SendForResponseAsync`, so other implementations must add it. |
| `3.0.0` | **Package renamed** `DeepSigma.DataAccess.Http` → `DeepSigma.DataAccess.Web` (package id, assembly, and root namespace), reflecting its expanded scope to all web transports. **New `IHttpApi` interface** extracted from `HttpApi` for mockability; `AddDeepSigmaHttp()` now also registers `IHttpApi`. **New shared `ReconnectPolicy`** (`DeepSigma.DataAccess.Web.Resilience`) factored out of `RetryAfterDelegatingHandler`'s backoff math, to be reused by the upcoming SSE and WebSocket reconnection loops — retry/backoff behavior is unchanged. **Migration:** update `using DeepSigma.DataAccess.Http*` namespaces to `…Web*`; the static deserializers live on `Serializer` (`Serializer.LoadFromJson`/`LoadFromXml`/`LoadFromCsv`). **New SSE client** — `ISseClient`/`SseClient` consume `text/event-stream` as an `IAsyncEnumerable` (typed or raw) via `System.Net.ServerSentEvents.SseParser`, with transparent reconnection, `Last-Event-ID` resumption, and event-type filtering; register with `AddDeepSigmaSse()`. **New WebSocket client** — `IWebSocketConnectionFactory`/`IWebSocketConnection` over `ClientWebSocket`: typed/raw duplex, a reassembled receive stream whose messages are a C# 15 union (`WebSocketMessage`), keep-alive, and auto-reconnect with an `OnReconnected` re-subscribe hook; register with `AddDeepSigmaWebSockets()`. **Unified DI** — `AddDeepSigmaWeb()` registers all three transports (or a selected subset). **Pluggable response guard (breaking)** — the `Note`/`Error Message` rate-limit check is no longer applied by default; opt in with a `JsonResponseGuard` on `Serializer.LoadFromJson` or the `HttpApi` constructor (in 3.0.0, `JsonResponseGuards.AlphaVantage`; removed in 4.0.0). |
| `1.3.0` | **Write verbs:** `POST`/`PUT`/`PATCH`/`DELETE` helpers on `HttpApi` — `PostJsonAsync` (typed and raw-string overloads), `PostFormAsync`, `PutJsonAsync`, `PatchJsonAsync`, `DeleteAsync` (all with the same `apiResultLoggingMethod` upstream-capture hook as the `GET` helpers); streaming-response variants `PostJsonToStreamAsync` / `PutJsonToStreamAsync`; and a full-control `SendAsync` / `SendToStreamAsync` escape hatch for caller-built `HttpRequestMessage`s. **Configurable JSON:** each instance carries a `JsonSerializerOptions` (used for both read and write) — new `DefaultJsonOptions` and `SnakeCaseJsonOptions` static presets, selectable via a new optional `jsonOptions` constructor argument; `LoadFromJson<T>` gains an optional `options` argument. Internal refactor routing every method through a shared request-based core (`SendForStringAsync` / `SendToStreamCoreAsync`). The new constructor and `LoadFromJson` parameters are optional, so existing call sites compile unchanged. |
| `1.2.0` | **Bug fix:** `RetryAfterDelegatingHandler` now clones the `HttpRequestMessage` between attempts so request bodies survive retries — previously the handler reused the same instance, which failed on `POST`/`PUT` because `HttpContent` is disposed after the first send. Internal refactor of `HttpApi.GetJsonResponseAsync` / `GetXmlResponseAsync` / `GetCsvDataAsync` to share a single body-fetch helper; `GetJsonResponseAsync` now uses `HttpCompletionOption.ResponseHeadersRead` matching the other variants. No public API changes. |
| `1.1.0` | XML methods on `HttpApi` (`GetDataFromXmlUrlAsync`, `GetXmlResponseAsync`, `StreamXmlElementsAsync`, `LoadFromXml`); streaming binary downloads (`DownloadToStreamAsync`, `DownloadToFileAsync`); `MinIntervalDelegatingHandler` and `RetryAfterDelegatingHandler` with `IHttpClientBuilder` extensions (`AddMinIntervalThrottle`, `AddRetryAfterPolicy`). Added `DeepSigma.Core` 1.3.0 dependency. |
| `1.0.0` | Initial release. JSON and CSV helpers on `HttpApi`. |

## License

MIT
