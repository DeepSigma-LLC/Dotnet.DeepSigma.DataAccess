using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace DeepSigma.DataAccess.API;

/// <summary>
/// Provides utility methods for interacting with web APIs, including fetching JSON and CSV data, deserializing JSON responses, and handling rate limits or error messages.
/// </summary>
public static class APIUtilities
{

    /// <summary>
    /// Fetches JSON data from a specified URL and deserializes it into an object of type T.
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <param name="url"></param>
    /// <param name="timeout_in_seconds"></param>
    /// <param name="ApiResultLoggingMethod"></param>
    /// <param name="cancel_token"></param>
    /// <returns></returns>
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
    /// Fetches JSON data from a specified URL and deserializes it into an object of type T.
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <param name="url"></param>
    /// <param name="timeout_in_seconds"></param>
    /// <param name="ApiResultLoggingMethod"></param>
    /// <param name="cancel_token"></param>
    /// <returns></returns>
    public static async Task<List<T>> GetDataFromCSVAsync<T>(string url, int timeout_in_seconds = 15, Action<string?>? ApiResultLoggingMethod = null, CancellationToken cancel_token = default) where T : class
    {
        string? csv = await GetCsvDataAsync(url, timeout_in_seconds, cancel_token);

        if (ApiResultLoggingMethod is not null)
        {
            ApiResultLoggingMethod(csv);
        }

        if (string.IsNullOrWhiteSpace(csv)) { return []; }
        List<T> results = LoadFromCSV<T>(csv);
        return results;
    }

    /// <summary>
    /// Fetches raw JSON data from a specified URL as a string.
    /// </summary>
    /// <param name="url_endpoint"></param>
    /// <param name="timeout_in_seconds"></param>
    /// <param name="cancel_token"></param>
    /// <returns></returns>
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
    /// Fetches CSV data from a specified URL as a string, ensuring the response is in CSV format.
    /// </summary>
    /// <param name="url_endpoint"></param>
    /// <param name="timeout_seconds"></param>
    /// <param name="cancel_token"></param>
    /// <returns></returns>
    /// <exception cref="InvalidOperationException"></exception>
    public static async Task<string?> GetCsvDataAsync(string url_endpoint, int timeout_seconds = 15, CancellationToken cancel_token = default)
    {
        using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(timeout_seconds) };
        using var resp = await http.GetAsync(url_endpoint, HttpCompletionOption.ResponseHeadersRead, cancel_token);
        resp.EnsureSuccessStatusCode();

        var text = await resp.Content.ReadAsStringAsync(cancel_token);

        // If they rate-limit you, they sometimes return text/JSON. We’ll sanity-check content-type.
        if (!resp.Content.Headers.ContentType?.MediaType?.Contains("csv", StringComparison.OrdinalIgnoreCase) ?? true)
        {
            
            throw new InvalidOperationException($"Non-CSV response: {text}");
        }
        return text;
    }

    /// <summary>
    /// Deserializes a JSON string into an object of type T, handling potential rate-limit or error messages from the API.
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <param name="json_text"></param>
    /// <returns></returns>
    /// <exception cref="InvalidOperationException"></exception>
    public static T? LoadFromJson<T>(string json_text)
    {
        if (string.IsNullOrWhiteSpace(json_text)) { return default; }

        JsonSerializerOptions opts = new()
        {
            NumberHandling = JsonNumberHandling.AllowReadingFromString,
            PropertyNameCaseInsensitive = true,
        };

        // Handle rate-limit / error messages
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
    /// Parses CSV text into a list of objects of type T using CsvHelper.
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <param name="csvText"></param>
    /// <returns></returns>
    public static List<T> LoadFromCSV<T>(string csvText) where T : class
    {
        IEnumerable<T> results = Utilities.CsvUtilities.LoadObjectsFromCSV<T>(csvText);
        return results.ToList();
    }
}
