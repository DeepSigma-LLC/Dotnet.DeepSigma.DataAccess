using System.Text.Json;
using System.Text.Json.Serialization;
using DeepSigma.DataAccess.CsvUtilities.Reading;
using DeepSigma.DataAccess.CsvUtilities.Results;

namespace DeepSigma.DataAccess.Http;

/// <summary>
/// Provides utility methods for interacting with web APIs: fetching JSON and CSV data,
/// deserializing JSON responses, and surfacing rate-limit / error payloads as exceptions.
/// </summary>
public static class APIUtilities
{
    /// <summary>
    /// Fetches JSON data from the URL and deserializes it into <typeparamref name="T"/>.
    /// </summary>
    public static async Task<T?> GetDataFromURLAsync<T>(string url, int timeout_in_seconds = 15, Action<string?>? ApiResultLoggingMethod = null, CancellationToken cancel_token = default)
    {
        string? json = await GetJsonResponseAsync(url, timeout_in_seconds, cancel_token);

        if (ApiResultLoggingMethod is not null)
        {
            ApiResultLoggingMethod(json);
        }

        if (string.IsNullOrWhiteSpace(json)) { return default; }
        T? results = LoadFromJson<T>(json);
        return results;
    }

    /// <summary>
    /// Fetches CSV data from the URL and deserializes it into a list of <typeparamref name="T"/>.
    /// </summary>
    public static async Task<List<T>> GetDataFromCSVAsync<T>(string url, int timeout_in_seconds = 15, Action<string?>? ApiResultLoggingMethod = null, CancellationToken cancel_token = default) where T : class
    {
        string? csv = await GetCsvDataAsync(url, timeout_in_seconds, cancel_token);

        if (ApiResultLoggingMethod is not null)
        {
            ApiResultLoggingMethod(csv);
        }

        if (string.IsNullOrWhiteSpace(csv)) { return []; }
        List<T> results = LoadFromCSV<T>(csv) ?? [];
        return results;
    }

    /// <summary>
    /// Fetches raw JSON from the URL as a string.
    /// </summary>
    public static async Task<string?> GetJsonResponseAsync(string url_endpoint, int timeout_in_seconds = 15, CancellationToken cancel_token = default)
    {
        Uri queryUri = new(url_endpoint);
        using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(timeout_in_seconds) };
        using var response = await http.GetAsync(queryUri, cancel_token);
        response.EnsureSuccessStatusCode();

        string json_text = await response.Content.ReadAsStringAsync(cancel_token);
        return json_text;
    }

    /// <summary>
    /// Fetches CSV data from the URL as a string, validating the Content-Type.
    /// </summary>
    public static async Task<string?> GetCsvDataAsync(string url_endpoint, int timeout_seconds = 15, CancellationToken cancel_token = default)
    {
        using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(timeout_seconds) };
        using var resp = await http.GetAsync(url_endpoint, HttpCompletionOption.ResponseHeadersRead, cancel_token);
        resp.EnsureSuccessStatusCode();

        var text = await resp.Content.ReadAsStringAsync(cancel_token);

        if (!resp.Content.Headers.ContentType?.MediaType?.Contains("csv", StringComparison.OrdinalIgnoreCase) ?? true)
        {
            throw new InvalidOperationException($"Non-CSV response: {text}");
        }
        return text;
    }

    /// <summary>
    /// Deserializes a JSON string into <typeparamref name="T"/>, surfacing API rate-limit / error payloads as exceptions.
    /// </summary>
    public static T? LoadFromJson<T>(string json_text)
    {
        if (string.IsNullOrWhiteSpace(json_text)) { return default; }

        JsonSerializerOptions opts = new()
        {
            NumberHandling = JsonNumberHandling.AllowReadingFromString,
            PropertyNameCaseInsensitive = true,
        };

        using var doc = JsonDocument.Parse(json_text);
        var root = doc.RootElement;

        if (root.TryGetProperty("Note", out var note))
        {
            throw new InvalidOperationException($"API note: {note.GetString()}");
        }

        if (root.TryGetProperty("Error Message", out var err))
        {
            throw new InvalidOperationException($"API error: {err.GetString()}");
        }

        T? dto = JsonSerializer.Deserialize<T>(json_text, opts);
        return dto;
    }

    /// <summary>
    /// Parses CSV text into a list of <typeparamref name="T"/>.
    /// </summary>
    public static List<T>? LoadFromCSV<T>(string csvText) where T : class
    {
        CsvImportResult<T> results = CsvReader.ReadFromStringSafe<T>(csvText);
        return results.IsSuccess ? results.Records : null;
    }
}
