using CsvHelper;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace DeepSigma.DataAccess.API
{
    public static class APIUtilities
    {

        public static async Task<T?> GetDataFromURLAsync<T>(string url, int timeout_in_seconds = 15, Action<string?>? JsonLoggingMethod = null, CancellationToken cancel_token = default)
        {
            string? json = await GetDataAsync(url, timeout_in_seconds, cancel_token);

            if (JsonLoggingMethod is not null)
            {
                JsonLoggingMethod(json);
            }

            if (string.IsNullOrWhiteSpace(json)) { return default; }
            T? results = await LoadFromJSON<T>(json, cancel_token);
            return results;
        }

        public static async Task<string?> GetDataAsync(string url_endpoint, int timeout_in_seconds = 15, CancellationToken cancel_token = default)
        {
            Uri queryUri = new(url_endpoint);
            using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(timeout_in_seconds) };
            using var response = await http.GetAsync(queryUri, cancel_token);
            response.EnsureSuccessStatusCode();

            string json_text = await response.Content.ReadAsStringAsync(cancel_token);
            return json_text;
        }

        public static async Task<T?> LoadFromJSON<T>(string json_text, CancellationToken cancel_token = default)
        {
            JsonSerializerOptions opts = new()
            {
                NumberHandling = JsonNumberHandling.AllowReadingFromString
            };

            Stream stream = new MemoryStream(Encoding.UTF8.GetBytes(json_text));
            T? dto = await JsonSerializer.DeserializeAsync<T>(stream, opts, cancellationToken: cancel_token);

            // Handle rate-limit / error messages
            using var doc = await JsonDocument.ParseAsync(stream, cancellationToken:cancel_token);
            var root = doc.RootElement;
            if (root.TryGetProperty("Note", out var note))
            {
                throw new InvalidOperationException($"API note: {note.GetString()}");
            }

            if (root.TryGetProperty("Error Message", out var err))
            {
                throw new InvalidOperationException($"API error: {err.GetString()}");
            }

            return dto;
        }

        public static async Task<string> GetCsvDataAsync<T>(string url_endpoint, int timeout_seconds = 15, CancellationToken cancel_token = default)
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

        public static List<T> ParseCsv<T>(string csvText)
        {
            using var reader = new StringReader(csvText);
            using var csv = new CsvReader(reader, CultureInfo.InvariantCulture);
            return csv.GetRecords<T>().ToList();
        }
    }
}
