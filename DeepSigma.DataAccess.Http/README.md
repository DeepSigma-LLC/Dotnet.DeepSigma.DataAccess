# DeepSigma.DataAccess.Http

HTTP helpers for fetching and deserialising **JSON** and **CSV** payloads from web APIs. Thin wrappers around `HttpClient` + `System.Text.Json` (JSON) and `DeepSigma.DataAccess.CsvUtilities` (CSV).

This package is **independent** of the relational stack.

## Installation

```bash
dotnet add package DeepSigma.DataAccess.Http
```

## Dependencies

| Package | Purpose |
|---|---|
| `DeepSigma.DataAccess.CsvUtilities` | CSV parsing used by `LoadFromCSV` / `GetDataFromCSVAsync`. |

Uses `System.Net.Http.HttpClient` and `System.Text.Json` from the BCL — no extra packages needed for those.

## What it provides

A single static class, `APIUtilities`, with two layers of helpers:

### High-level: fetch + deserialise in one call

| Method | Behaviour |
|---|---|
| `GetDataFromURLAsync<T>(url, timeoutSeconds = 15, loggingMethod = null, ct = default)` | `GET`s the URL, deserialises the JSON body into `T`. Optionally hands the raw body to a logging callback first. |
| `GetDataFromCSVAsync<T>(url, timeoutSeconds = 15, loggingMethod = null, ct = default)` | `GET`s the URL, parses the CSV body into `List<T>` via `DeepSigma.DataAccess.CsvUtilities`. Optionally logs raw body. |

### Low-level: fetch and deserialise separately

| Method | Behaviour |
|---|---|
| `GetJsonResponseAsync(url, timeoutSeconds = 15, ct = default)` | `GET`s the URL and returns the raw body as a string. Throws on non-success status. |
| `GetCsvDataAsync(url, timeoutSeconds = 15, ct = default)` | Same as above, but additionally validates that the `Content-Type` looks like a CSV. Throws if it does not. |
| `LoadFromJson<T>(jsonText)` | Deserialises JSON text into `T`. Throws on the presence of `Note` or `Error Message` properties (a common rate-limit pattern). |
| `LoadFromCSV<T>(csvText)` | Parses CSV text into `List<T>?`. Returns `null` if parsing fails. |

JSON deserialisation uses these `JsonSerializerOptions`:

- `NumberHandling = AllowReadingFromString` — accepts numeric values quoted as strings.
- `PropertyNameCaseInsensitive = true` — matches camelCase JSON to PascalCase properties without `[JsonPropertyName]` attributes.

## Quick start: JSON

```csharp
using DeepSigma.DataAccess.Http;

public sealed class WeatherResponse
{
    public string City { get; set; } = "";
    public double TempF { get; set; }
}

WeatherResponse? weather = await APIUtilities.GetDataFromURLAsync<WeatherResponse>(
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

List<Trade> trades = await APIUtilities.GetDataFromCSVAsync<Trade>(
    "https://example.com/data/trades.csv");
```

## Quick start: separate fetch + parse

If you want to log, cache, or transform the raw body before parsing:

```csharp
string? raw = await APIUtilities.GetJsonResponseAsync(url);
File.WriteAllText("snapshot.json", raw);          // cache
WeatherResponse? dto = APIUtilities.LoadFromJson<WeatherResponse>(raw!);
```

## Notes

- **Rate-limit detection.** `LoadFromJson` throws `InvalidOperationException` if the root JSON object contains a `Note` or `Error Message` property. This pattern is used by financial-data providers (e.g. Alpha Vantage) to surface throttling responses with HTTP 200. If your target API does not follow that convention, this check is harmless — those properties simply won't appear.
- **HttpClient lifetime.** Every call constructs and disposes a fresh `HttpClient`. This is fine for occasional calls; for hot paths consider lifting to an `IHttpClientFactory`-managed client to avoid socket exhaustion under load.
- **CSV content-type check.** `GetCsvDataAsync` validates that the response `Content-Type` contains the substring `"csv"`. If the server returns the data with a generic `text/plain` or `application/octet-stream`, the call will throw — use `GetJsonResponseAsync` + `LoadFromCSV` to bypass the check.
- **Timeouts.** All helpers default to a 15-second timeout. Increase it for slow downloads, decrease it for interactive paths.

## License

MIT
