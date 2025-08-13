using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Dotnet.DeepSigma.DataAccess.API.AlphaVantage
{
    public class FundamentalData
    {
        private string api_key { get; }

        public FundamentalData(string api_key)
        {
            this.api_key = api_key;
        }

        public async Task<T?> GetInsiderTransactions<T>(string symbol, CancellationToken ct = default)
        {
            string url = $"https://www.alphavantage.co/query?function=INSIDER_TRANSACTIONS&symbol={symbol}&apikey={api_key}";
            var results = await APIUtilities.GetDataAsync<T>(url, cancel_token: ct);
            return results;
        }

        public async Task<T?> GetEarningsTranscript<T>(string symbol, int year, int quarter, CancellationToken ct = default)
        {
            string url = $"https://www.alphavantage.co/query?function=EARNINGS_CALL_TRANSCRIPT&symbol={symbol}&quarter={year}Q{quarter}&apikey={api_key}";
            var results = await APIUtilities.GetDataAsync<T>(url, cancel_token: ct);
            return results;
        }


    }
}
