using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using DeepSigma.DataAccess.API.AlphaVantage.Enums;

namespace DeepSigma.DataAccess.API.AlphaVantage
{
    public class CommodityData
    {

        private string api_key { get; }

        internal CommodityData(string api_key)
        {
            this.api_key = api_key;
        }

        public async Task<T?> GetWTI<T>(TimeSeriesInterval interval = TimeSeriesInterval.Daily, CancellationToken ct = default)
        {
            string url = $"https://www.alphavantage.co/query?function=WTI&interval={interval.ToString()}&apikey={api_key}";
            T? results = await APIUtilities.GetDataAsync<T>(url, cancel_token: ct);
            return results;
        }

        public async Task<T?> GetBrent<T>(TimeSeriesInterval interval = TimeSeriesInterval.Daily, CancellationToken ct = default)
        {
            string url = $"https://www.alphavantage.co/query?function=BRENT&interval={interval.ToString()}&apikey={api_key}";
            T? results = await APIUtilities.GetDataAsync<T>(url, cancel_token: ct);
            return results;
        }


        public async Task<T?> GetNaturalGas<T>(TimeSeriesInterval interval = TimeSeriesInterval.Daily, CancellationToken ct = default)
        {
            string url = $"https://www.alphavantage.co/query?function=NATURAL_GAS&interval={interval.ToString()}&apikey={api_key}";
            T? results = await APIUtilities.GetDataAsync<T>(url, cancel_token: ct);
            return results;
        }


        public async Task<T?> GetCopper<T>(TimeSeriesInterval interval = TimeSeriesInterval.Daily, CancellationToken ct = default)
        {
            string url = $"https://www.alphavantage.co/query?function=COPPER&interval={interval.ToString()}&apikey={api_key}";
            T? results = await APIUtilities.GetDataAsync<T>(url, cancel_token: ct);
            return results;
        }


        public async Task<T?> GetAluminum<T>(TimeSeriesInterval interval = TimeSeriesInterval.Daily, CancellationToken ct = default)
        {
            string url = $"https://www.alphavantage.co/query?function=ALUMINUM&interval={interval.ToString()}&apikey={api_key}";
            T? results = await APIUtilities.GetDataAsync<T>(url, cancel_token: ct);
            return results;
        }

        public async Task<T?> GetWheat<T>(TimeSeriesInterval interval = TimeSeriesInterval.Daily, CancellationToken ct = default)
        {
            string url = $"https://www.alphavantage.co/query?function=WHEAT&interval={interval.ToString()}&apikey={api_key}";
            T? results = await APIUtilities.GetDataAsync<T>(url, cancel_token: ct);
            return results;
        }

        public async Task<T?> GetCorn<T>(TimeSeriesInterval interval = TimeSeriesInterval.Daily, CancellationToken ct = default)
        {
            string url = $"https://www.alphavantage.co/query?function=CORN&interval={interval.ToString()}&apikey={api_key}";
            T? results = await APIUtilities.GetDataAsync<T>(url, cancel_token: ct);
            return results;
        }

        public async Task<T?> GetCotton<T>(TimeSeriesInterval interval = TimeSeriesInterval.Daily, CancellationToken ct = default)
        {
            string url = $"https://www.alphavantage.co/query?function=COTTON&interval={interval.ToString()}&apikey={api_key}";
            T? results = await APIUtilities.GetDataAsync<T>(url, cancel_token: ct);
            return results;
        }
        public async Task<T?> GetSugar<T>(TimeSeriesInterval interval = TimeSeriesInterval.Daily, CancellationToken ct = default)
        {
            string url = $"https://www.alphavantage.co/query?function=SUGAR&interval={interval.ToString()}&apikey={api_key}";
            T? results = await APIUtilities.GetDataAsync<T>(url, cancel_token: ct);
            return results;
        }
        public async Task<T?> GetCoffee<T>(TimeSeriesInterval interval = TimeSeriesInterval.Daily, CancellationToken ct = default)
        {
            string url = $"https://www.alphavantage.co/query?function=COFFEE&interval={interval.ToString()}&apikey={api_key}";
            T? results = await APIUtilities.GetDataAsync<T>(url, cancel_token: ct);
            return results;
        }

        public async Task<T?> GetGlobalCommodityIndex<T>(TimeSeriesInterval interval = TimeSeriesInterval.Daily, CancellationToken ct = default)
        {
            string url = $"https://www.alphavantage.co/query?function=ALL_COMMODITIES&interval={interval.ToString()}&apikey={api_key}";
            T? results = await APIUtilities.GetDataAsync<T>(url, cancel_token: ct);
            return results;
        }

    }
}
