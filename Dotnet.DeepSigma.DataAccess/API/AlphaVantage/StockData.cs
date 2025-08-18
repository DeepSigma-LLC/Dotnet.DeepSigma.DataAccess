using DeepSigma.DataAccess.API.AlphaVantage.Enums;
using DeepSigma.General.Extensions;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DeepSigma.DataAccess.API.AlphaVantage
{
    public class StockData
    {
        private string api_key { get; set; }

        internal StockData(string api_key)
        {
            this.api_key = api_key;
        }

        public async Task<T?> GetIntradayTimeSeriesData<T>(string symbol, TimeSeriesIntradayInterval interval = TimeSeriesIntradayInterval.Fifteen, OutputSize output_size = OutputSize.Full, CancellationToken ct = default)
        {
            string url = $"https://www.alphavantage.co/query?function=TIME_SERIES_INTRADAY&symbol={symbol}&interval={interval.ToDescriptionString()}&apikey={api_key}&outputsize={output_size.ToDescriptionString()}";
            T? results = await APIUtilities.GetDataAsync<T>(url, cancel_token: ct);
            return results;
        }

        public async Task<T?> GetDailyTimeSeriesData<T>(string symbol, OutputSize output_size = OutputSize.Full, CancellationToken ct = default)
        {
            string url = $"https://www.alphavantage.co/query?function=TIME_SERIES_DAILY_ADJUSTED&symbol={symbol}&outputsize={output_size.ToDescriptionString()}&apikey={api_key}";
            var results = await APIUtilities.GetDataAsync<T>(url, cancel_token: ct);
            return results;
        }

        public async Task<T?> GetWeeklyTimeSeriesData<T>(string symbol, CancellationToken ct = default)
        {
            string url = $"https://www.alphavantage.co/query?function=TIME_SERIES_WEEKLY_ADJUSTED&symbol={symbol}&apikey={api_key}";
            var results = await APIUtilities.GetDataAsync<T>(url, cancel_token: ct);
            return results;
        }

        public async Task<T?> GetMonthlyTimeSeriesData<T>(string symbol, CancellationToken ct = default)
        {
            string url = $"https://www.alphavantage.co/query?function=TIME_SERIES_MONTHLY_ADJUSTED&symbol={symbol}&apikey={api_key}";
            var results = await APIUtilities.GetDataAsync<T>(url, cancel_token: ct);
            return results;
        }

        public async Task<T?> GetBulkRealTimeQuoteData<T>(string[] symbols, CancellationToken ct = default)
        {
            string url = $"https://www.alphavantage.co/query?function=REALTIME_BULK_QUOTES&symbol={string.Join(",", symbols)}&apikey={api_key}";
            var results = await APIUtilities.GetDataAsync<T>(url, cancel_token: ct);
            return results;
        }

    }
}
