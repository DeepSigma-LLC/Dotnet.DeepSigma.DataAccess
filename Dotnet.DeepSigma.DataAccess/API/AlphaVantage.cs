using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Security.Policy;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace Dotnet.DeepSigma.DataAccess.API
{
    public class AlphaVantage
    {
        private string api_key {  get; set; }
        public AlphaVantage(string api_key = "demo")
        {
            this.api_key = api_key;
        }

        public async Task<dynamic?> GetDailyPrices(string symbol, CancellationToken ct = default, int timeout_in_seconds = 15)
        {
            string query = $"https://www.alphavantage.co/query?function=TIME_SERIES_DAILY&symbol={symbol}&apikey={api_key}";
            Uri queryUri = new Uri(query);

            using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(timeout_in_seconds) };
            using var response = await http.GetAsync(queryUri, ct);
            response.EnsureSuccessStatusCode();

            string json_text = await response.Content.ReadAsStringAsync(ct);
            dynamic? json_data = JsonSerializer.Deserialize<Dictionary<string, dynamic>>(json_text);
            return json_data;
        }
    }
}
