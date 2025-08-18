using DeepSigma.DataAccess.API.AlphaVantage.Enums;
using DeepSigma.General.Extensions;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DeepSigma.DataAccess.API.AlphaVantage
{
    public class EconomicData
    {
        private string api_key { get; }

        internal EconomicData(string api_key)
        {
            this.api_key = api_key;
        }

        public async Task<T?> GetRealGDP<T>(TimeSeriesInterval interval = TimeSeriesInterval.Daily, CancellationToken ct = default)
        {
            string url = $"https://www.alphavantage.co/query?function=REAL_GDP&interval={interval.ToDescriptionString()}&apikey={api_key}";
            T? results = await APIUtilities.GetDataAsync<T>(url, cancel_token: ct);
            return results;
        }

        public async Task<T?> GetRealGDPPerCapita<T>(TimeSeriesInterval interval = TimeSeriesInterval.Daily, CancellationToken ct = default)
        {
            string url = $"https://www.alphavantage.co/query?function=REAL_GDP_PER_CAPITA&interval={interval.ToDescriptionString()}&apikey={api_key}";
            T? results = await APIUtilities.GetDataAsync<T>(url, cancel_token: ct);
            return results;
        }

        public async Task<T?> GetUSTreasuryYield<T>(FixedIncomeMaturities maturity,TimeSeriesInterval interval = TimeSeriesInterval.Daily, CancellationToken ct = default)
        {
            string url = $"https://www.alphavantage.co/query?function=TREASURY_YIELD&interval={interval.ToDescriptionString()}&maturity={maturity.ToDescriptionString()}&apikey={api_key}";
            T? results = await APIUtilities.GetDataAsync<T>(url, cancel_token: ct);
            return results;
        }

        public async Task<T?> GetFedFunds<T>(TimeSeriesInterval interval = TimeSeriesInterval.Daily, CancellationToken ct = default)
        {
            string url = $"https://www.alphavantage.co/query?function=FEDERAL_FUNDS_RATE&interval={interval.ToDescriptionString()}&apikey={api_key}";
            T? results = await APIUtilities.GetDataAsync<T>(url, cancel_token: ct);
            return results;
        }


        public async Task<T?> GetCPI<T>(TimeSeriesInterval interval = TimeSeriesInterval.Daily, CancellationToken ct = default)
        {
            string url = $"https://www.alphavantage.co/query?function=CPI&interval={interval.ToDescriptionString()}&apikey={api_key}";
            T? results = await APIUtilities.GetDataAsync<T>(url, cancel_token: ct);
            return results;
        }



        public async Task<T?> GetInflation<T>(CancellationToken ct = default)
        {
            string url = $"https://www.alphavantage.co/query?function=INFLATION&apikey={api_key}";
            T? results = await APIUtilities.GetDataAsync<T>(url, cancel_token: ct);
            return results;
        }


        public async Task<T?> GetRetailSales<T>(CancellationToken ct = default)
        {
            string url = $"https://www.alphavantage.co/query?function=RETAIL_SALES&apikey={api_key}";
            T? results = await APIUtilities.GetDataAsync<T>(url, cancel_token: ct);
            return results;
        }

        public async Task<T?> GetDurableGoodsOrders<T>(CancellationToken ct = default)
        {
            string url = $"https://www.alphavantage.co/query?function=DURABLES&apikey={api_key}";
            T? results = await APIUtilities.GetDataAsync<T>(url, cancel_token: ct);
            return results;
        }

        public async Task<T?> GetNonFarmPayrolls<T>(CancellationToken ct = default)
        {
            string url = $"https://www.alphavantage.co/query?function=NONFARM_PAYROLL&apikey={api_key}";
            T? results = await APIUtilities.GetDataAsync<T>(url, cancel_token: ct);
            return results;
        }

        public async Task<T?> GetUnemployment<T>(CancellationToken ct = default)
        {
            string url = $"https://www.alphavantage.co/query?function=UNEMPLOYMENT&apikey={api_key}";
            T? results = await APIUtilities.GetDataAsync<T>(url, cancel_token: ct);
            return results;
        }    

    }
}
