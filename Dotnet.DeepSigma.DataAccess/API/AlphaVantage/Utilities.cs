using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DeepSigma.DataAccess.API.AlphaVantage
{
    public class Utilities
    {
        private string api_key { get; set; }
        internal Utilities(string api_key = "demo")
        {
            this.api_key = api_key;
        }

        public async Task<T?> GetTickerSearch<T>(string keyword, CancellationToken ct = default)
        {
            string url = $"https://www.alphavantage.co/query?function=SYMBOL_SEARCH&keywords={keyword}&apikey={api_key}";
            var results = await APIUtilities.GetDataAsync<T>(url, cancel_token: ct);
            return results;
        }


        public async Task<T?> GetGlobalMarketStatus<T>(CancellationToken ct = default)
        {
            string url = $"https://www.alphavantage.co/query?function=MARKET_STATUS&apikey={api_key}";
            var results = await APIUtilities.GetDataAsync<T>(url, cancel_token: ct);
            return results;
        }

        public async Task<T?> GetTopGainersAndLosers<T>(CancellationToken ct = default)
        {
            string url = $"https://www.alphavantage.co/query?function=TOP_GAINERS_LOSERS&apikey={api_key}";
            var results = await APIUtilities.GetDataAsync<T>(url, cancel_token: ct);
            return results;
        }

        public async Task<T?> GetHistroicalNews<T>(string[] symbols, DateTime? StartDate, DateTime? EndDate, int limit = 1000, CancellationToken ct = default)
        {
            string url = $"https://www.alphavantage.co/query?function=NEWS_SENTIMENT&tickers={string.Join(",", symbols)}&limit={limit}&sort=LATEST&apikey={api_key}";
            
            if(StartDate.HasValue)
            {
                url += $"&time_from={StartDate.Value.ToUniversalTime().ToString()}";
            }

            if(EndDate.HasValue)
            {
                url += $"&time_to={EndDate.Value.ToUniversalTime().ToString()}";
            }

            var results = await APIUtilities.GetDataAsync<T>(url, cancel_token: ct);
            return results;
        }

        public async Task<T?> GetNews<T>(string[] symbols, int limit = 1000, CancellationToken ct = default)
        {
            string url = $"https://www.alphavantage.co/query?function=NEWS_SENTIMENT&tickers={string.Join(",", symbols)}&sort=LATEST&apikey=demo&apikey={api_key}";
            var results = await APIUtilities.GetDataAsync<T>(url, cancel_token: ct);
            return results;
        }

   


    }
}
