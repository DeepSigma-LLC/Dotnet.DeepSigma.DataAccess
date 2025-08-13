using Dotnet.DeepSigma.DataAccess.API.AlphaVantage.Enums;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Dotnet.DeepSigma.DataAccess.API.AlphaVantage
{
    public class OptionData
    {

        private string api_key { get; }

        public OptionData(string api_key)
        {
            this.api_key = api_key;
        }

        public async Task<T?> GetRealTimeOptionData<T>(string symbol, CancellationToken ct = default)
        {
            string url = $"https://www.alphavantage.co/query?function=REALTIME_OPTIONS&symbol={symbol}&require_greeks=true&apikey={api_key}";
            T? results = await APIUtilities.GetDataAsync<T>(url, cancel_token: ct);
            return results;
        }

        public async Task<T?> GetHistoricalOptionData<T>(string symbol, DateOnly date, CancellationToken ct = default)
        {
            string url = $"https://www.alphavantage.co/query?function=HISTORICAL_OPTIONS&symbol={symbol}&date={date.ToString("D")}&apikey={api_key}";
            T? results = await APIUtilities.GetDataAsync<T>(url, cancel_token: ct);
            return results;
        }

    }
}
