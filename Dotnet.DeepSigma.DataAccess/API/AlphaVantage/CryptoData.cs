using DeepSigma.DataAccess.API.AlphaVantage.Enums;
using DeepSigma.General.Extensions;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DeepSigma.DataAccess.API.AlphaVantage
{
    public class CryptoData
    {
        private string api_key { get; }

        internal CryptoData(string api_key)
        {
            this.api_key = api_key;
        }

        public async Task<T?> GetExchangeRate<T>(string base_currency, string local_currnecy, CancellationToken ct = default)
        {
            string url = $"https://www.alphavantage.co/query?function=CURRENCY_EXCHANGE_RATE&from_currency={local_currnecy}&to_currency={base_currency}&apikey={api_key}";
            T? results = await APIUtilities.GetDataAsync<T>(url, cancel_token: ct);
            return results;
        }

        public async Task<T?> GetExchangeRatesIntraday<T>(string symbol, string currency, TimeSeriesIntradayInterval interval = TimeSeriesIntradayInterval.Fifteen, OutputSize output_size = OutputSize.Full, CancellationToken ct = default)
        {
            string url = $"https://www.alphavantage.co/query?function=CRYPTO_INTRADAY&symbol={symbol}&market={currency}&interval={interval.ToDescriptionString()}&outputsize={output_size.ToDescriptionString()}&apikey={api_key}";
            T? results = await APIUtilities.GetDataAsync<T>(url, cancel_token: ct);
            return results;
        }

        public async Task<T?> GetExchangeRatesDaily<T>(string symbol, string currency, OutputSize output_size = OutputSize.Full, CancellationToken ct = default)
        {
            string url = $"https://www.alphavantage.co/query?function=DIGITAL_CURRENCY_DAILY&symbol={symbol}&market={currency}&outputsize={output_size.ToDescriptionString()}&apikey={api_key}";
            T? results = await APIUtilities.GetDataAsync<T>(url, cancel_token: ct);
            return results;
        }

        public async Task<T?> GetExchangeRatesWeekly<T>(string symbol, string currency, OutputSize output_size = OutputSize.Full, CancellationToken ct = default)
        {
            string url = $"https://www.alphavantage.co/query?function=DIGITAL_CURRENCY_WEEKLY&symbol={symbol}&market={currency}&apikey={api_key}";
            T? results = await APIUtilities.GetDataAsync<T>(url, cancel_token: ct);
            return results;
        }

        public async Task<T?> GetExchangeRatesMonthly<T>(string symbol, string currency, OutputSize output_size = OutputSize.Full, CancellationToken ct = default)
        {
            string url = $"https://www.alphavantage.co/query?function=DIGITAL_CURRENCY_MONTHLY&symbol={symbol}&market={currency}&apikey={api_key}";
            T? results = await APIUtilities.GetDataAsync<T>(url, cancel_token: ct);
            return results;
        }

    }
}
