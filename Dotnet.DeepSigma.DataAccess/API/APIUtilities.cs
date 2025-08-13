using CsvHelper;
using System;
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
        public static async Task<dynamic?> GetDataAsDynamic(string url_endpoint, int timeout_in_seconds = 15, CancellationToken cancel_token = default)
        {
            Uri queryUri = new Uri(url_endpoint);

            using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(timeout_in_seconds) };
            using var response = await http.GetAsync(queryUri, cancel_token);
            response.EnsureSuccessStatusCode();

            string json_text = await response.Content.ReadAsStringAsync(cancel_token);
            dynamic? json_data = JsonSerializer.Deserialize<Dictionary<string, dynamic>>(json_text);
            return json_data;
        }

        public static async Task<T?> GetDataAsync<T>(string url_endpoint, int timeout_seconds = 15, CancellationToken cancel_token = default)
        {
            using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(timeout_seconds) };
            using var resp = await http.GetAsync(url_endpoint, cancel_token);
            resp.EnsureSuccessStatusCode();

            var json = await resp.Content.ReadAsStringAsync(cancel_token);

            JsonSerializerOptions opts = new()
            {
                NumberHandling = JsonNumberHandling.AllowReadingFromString
            };

            var dto = JsonSerializer.Deserialize<T>(json, opts);

            // Handle rate-limit / error messages
            using var doc = JsonDocument.Parse(json);
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

        public static async Task<List<T>> GetCsvDataAsync<T>(string url_endpoint, int timeout_seconds = 15, CancellationToken cancel_token = default)
        {
            using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(timeout_seconds) };
            using var resp = await http.GetAsync(url_endpoint, HttpCompletionOption.ResponseHeadersRead, cancel_token);
            resp.EnsureSuccessStatusCode();

            // If they rate-limit you, they sometimes return text/JSON. We’ll sanity-check content-type.
            if (!resp.Content.Headers.ContentType?.MediaType?.Contains("csv", StringComparison.OrdinalIgnoreCase) ?? true)
            {
                var text = await resp.Content.ReadAsStringAsync(cancel_token);
                throw new InvalidOperationException($"Non-CSV response: {text}");
            }

            await using var stream = await resp.Content.ReadAsStreamAsync(cancel_token);
            using var reader = new StreamReader(stream);
            using var csv = new CsvReader(reader, CultureInfo.InvariantCulture);

            // Alpha Vantage uses dots as decimal separator; InvariantCulture is correct.
            var rows = csv.GetRecords<T>().ToList(); // streaming under the hood; materialize to List
            return rows;
        }
    }
}
