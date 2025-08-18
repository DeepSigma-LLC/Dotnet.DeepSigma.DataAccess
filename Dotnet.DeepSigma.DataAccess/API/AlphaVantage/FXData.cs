using DeepSigma.DataAccess.API.AlphaVantage.Enums;
using DeepSigma.General.Extensions;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DeepSigma.DataAccess.API.AlphaVantage
{
    public class FXData
    {
        private string api_key { get; }

        internal FXData(string api_key)
        {
            this.api_key = api_key;
        }

        public async Task<T?> GetExchangeRate<T>(string base_currency, string local_currnecy, CancellationToken ct = default)
        {
            string url = $"https://www.alphavantage.co/query?function=CURRENCY_EXCHANGE_RATE&from_currency={local_currnecy}&to_currency={base_currency}&apikey={api_key}";
            T? results = await APIUtilities.GetDataAsync<T>(url, cancel_token: ct);
            return results;
        }

        public async Task<T?> GetExchangeRatesIntraday<T>(string base_currency, string local_currnecy, TimeSeriesIntradayInterval interval = TimeSeriesIntradayInterval.Fifteen, OutputSize output_size = OutputSize.Full, CancellationToken ct = default)
        {
            string url = $"https://www.alphavantage.co/query?function=FX_INTRADAY&from_currency={local_currnecy}&to_currency={base_currency}&interval={interval.ToDescriptionString()}&outputsize={output_size.ToDescriptionString()}&apikey={api_key}";
            T? results = await APIUtilities.GetDataAsync<T>(url, cancel_token: ct);
            return results;
        }

        public async Task<T?> GetExchangeRatesDaily<T>(string base_currency, string local_currnecy, OutputSize output_size = OutputSize.Full, CancellationToken ct = default)
        {
            string url = $"https://www.alphavantage.co/query?function=FX_DAILY&from_currency={local_currnecy}&to_currency={base_currency}&outputsize={output_size.ToDescriptionString()}&apikey={api_key}";
            T? results = await APIUtilities.GetDataAsync<T>(url, cancel_token: ct);
            return results;
        }


        public async Task<T?> GetExchangeRatesWeekly<T>(string base_currency, string local_currnecy, CancellationToken ct = default)
        {
            string url = $"https://www.alphavantage.co/query?function=FX_WEEKLY&from_currency={local_currnecy}&to_currency={base_currency}&apikey={api_key}";
            T? results = await APIUtilities.GetDataAsync<T>(url, cancel_token: ct);
            return results;
        }

        public async Task<T?> GetExchangeRatesMonthly<T>(string base_currency, string local_currnecy, CancellationToken ct = default)
        {
            string url = $"https://www.alphavantage.co/query?function=FX_MONTHLY&from_currency={local_currnecy}&to_currency={base_currency}&apikey={api_key}";
            T? results = await APIUtilities.GetDataAsync<T>(url, cancel_token: ct);
            return results;
        }

    }
}
