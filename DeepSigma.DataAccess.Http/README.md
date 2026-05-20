# DeepSigma.DataAccess.Http

HTTP helpers for fetching and deserialising **JSON** and **CSV** payloads from web APIs. Thin instance wrappers around `HttpClient` + `System.Text.Json` (JSON) and `DeepSigma.DataAccess.CsvUtilities` (CSV).

This package is **independent** of the relational stack.

## Installation

```bash
dotnet add package DeepSigma.DataAccess.Http
```

## Dependencies

| Package | Purpose |
|---|---|
| `DeepSigma.DataAccess.CsvUtilities` | CSV parsing used by `LoadFromCsv` / `GetDataFromCsvAsync`. |
| `Microsoft.Extensions.Http` | `IHttpClientFactory` integration for the DI extension. |

Uses `System.Net.Http.HttpClient` and `System.Text.Json` from the BCL — no extra packages needed for those.

## What it provides

An instance class `HttpApi` with two layers of helpers, plus two pure static helpers that don't need an `HttpClient`.

### Instance methods (require an `HttpClient` via constructor)

| Method | Behaviour |
|---|---|
| `GetDataFromUrlAsync<T>(url, timeoutInSeconds = 15, apiResultLoggingMethod = null, cancellationToken = default)` | `GET`s the URL, deserialises the JSON body into `T`. Optionally hands the raw body to a logging callback first. |
| `GetDataFromCsvAsync<T>(url, timeoutInSeconds = 15, apiResultLoggingMethod = null, cancellationToken = default)` | `GET`s the URL, parses the CSV body into `List<T>` via `DeepSigma.DataAccess.CsvUtilities`. |
| `GetJsonResponseAsync(url, timeoutInSeconds = 15, cancellationToken = default)` | `GET`s the URL and returns the raw body as a string. Throws on non-success status. |
| `GetCsvDataAsync(url, timeoutSeconds = 15, cancellationToken = default)` | Same as above, but additionally validates that the `Content-Type` looks like a CSV. Throws if it does not. |

### Static helpers (pure — no `HttpClient` needed)

| Method | Behaviour |
|---|---|
| `HttpApi.LoadFromJson<T>(jsonText)` | Deserialises JSON text into `T`. Throws on the presence of `Note` or `Error Message` properties (a common rate-limit pattern). |
| `HttpApi.LoadFromCsv<T>(csvText)` | Parses CSV text into `List<T>?`. Returns `null` if parsing fails. |

JSON deserialisation uses these `JsonSerializerOptions`:

- `NumberHandling = AllowReadingFromString` — accepts numeric values quoted as strings.
- `PropertyNameCaseInsensitive = true` — matches camelCase JSON to PascalCase properties without `[JsonPropertyName]` attributes.

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

Then inject `HttpApi` into your services:

```csharp
public class WeatherService(HttpApi http)
{
    public Task<WeatherResponse?> GetForCity(string city, CancellationToken ct)
        => http.GetDataFromUrlAsync<WeatherResponse>($"weather?city={city}", cancellationToken: ct);
}
```

## Quick start: JSON

```csharp
using DeepSigma.DataAccess.Http;

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
using DeepSigma.DataAccess.Http;

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
WeatherResponse? dto = HttpApi.LoadFromJson<WeatherResponse>(raw!);
```

## Notes

- **Rate-limit detection.** `LoadFromJson` throws `InvalidOperationException` if the root JSON object contains a `Note` or `Error Message` property. This pattern is used by financial-data providers (e.g. Alpha Vantage) to surface throttling responses with HTTP 200. If your target API does not follow that convention, this check is harmless — those properties simply won't appear.
- **HttpClient lifetime.** When you register via `AddDeepSigmaHttp()`, `IHttpClientFactory` manages the underlying `HttpClient` (handler pooling, DNS rotation, etc.). When you construct `HttpApi` directly, you control the `HttpClient` lifetime yourself — share a long-lived instance across your application rather than constructing one per call.
- **Per-call timeouts.** Enforced via a linked `CancellationTokenSource` independently of the injected `HttpClient.Timeout`. The factory-managed client's own timeout is ignored.
- **Cancellation.** Every instance method accepts an optional `CancellationToken` and links it with the timeout-driven CTS so either trigger will cancel the request.
- **CSV content-type check.** `GetCsvDataAsync` validates that the response `Content-Type` contains the substring `"csv"`. If the server returns the data with a generic `text/plain` or `application/octet-stream`, the call will throw — use `GetJsonResponseAsync` + `LoadFromCsv` to bypass the check.

## License

MIT
