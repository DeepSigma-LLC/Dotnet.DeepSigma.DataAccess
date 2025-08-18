using DeepSigma.DataAccess.API.AlphaVantage.Enums;
using DeepSigma.General.Extensions;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static System.Runtime.InteropServices.JavaScript.JSType;

namespace DeepSigma.DataAccess.API.AlphaVantage
{
    public class FundamentalData
    {
        private string api_key { get; }

        internal FundamentalData(string api_key)
        {
            this.api_key = api_key;
        }

        public async Task<T?> GetInsiderTransactions<T>(string symbol, CancellationToken ct = default)
        {
            string url = $"https://www.alphavantage.co/query?function=INSIDER_TRANSACTIONS&symbol={symbol}&apikey={api_key}";
            var results = await APIUtilities.GetDataAsync<T>(url, cancel_token: ct);
            return results;
        }

        public async Task<T?> GetEarningsTranscript<T>(string symbol, int year, byte quarter, CancellationToken ct = default)
        {
            string url = $"https://www.alphavantage.co/query?function=EARNINGS_CALL_TRANSCRIPT&symbol={symbol}&quarter={year}Q{quarter}&apikey={api_key}";
            var results = await APIUtilities.GetDataAsync<T>(url, cancel_token: ct);
            return results;
        }

        public async Task<T?> GetCompanyOverview<T>(string symbol, CancellationToken ct = default)
        {
            string url = $"https://www.alphavantage.co/query?function=OVERVIEW&symbol={symbol}&apikey={api_key}";
            var results = await APIUtilities.GetDataAsync<T>(url, cancel_token: ct);
            return results;
        }


        public async Task<T?> GetDividends<T>(string symbol, CancellationToken ct = default)
        {
            string url = $"https://www.alphavantage.co/query?function=DIVIDENDS&symbol={symbol}&apikey={api_key}";
            var results = await APIUtilities.GetDataAsync<T>(url, cancel_token: ct);
            return results;
        }


        public async Task<T?> GetStockSplits<T>(string symbol, CancellationToken ct = default)
        {
            string url = $"https://www.alphavantage.co/query?function=SPLITS&symbol={symbol}&apikey={api_key}";
            var results = await APIUtilities.GetDataAsync<T>(url, cancel_token: ct);
            return results;
        }



        public async Task<T?> GetIncomeStatement<T>(string symbol, CancellationToken ct = default)
        {
            string url = $"https://www.alphavantage.co/query?function=INCOME_STATEMENT&symbol={symbol}&apikey={api_key}";
            var results = await APIUtilities.GetDataAsync<T>(url, cancel_token: ct);
            return results;
        }

        public async Task<T?> GetBalanceSheet<T>(string symbol, CancellationToken ct = default)
        {
            string url = $"https://www.alphavantage.co/query?function=BALANCE_SHEET&symbol={symbol}&apikey={api_key}";
            var results = await APIUtilities.GetDataAsync<T>(url, cancel_token: ct);
            return results;
        }

        public async Task<T?> GetCashFlow<T>(string symbol, CancellationToken ct = default)
        {
            string url = $"https://www.alphavantage.co/query?function=CASH_FLOW&symbol={symbol}&apikey={api_key}";
            var results = await APIUtilities.GetDataAsync<T>(url, cancel_token: ct);
            return results;
        }

        public async Task<T?> GetEarningsHistory<T>(string symbol, CancellationToken ct = default)
        {
            string url = $"https://www.alphavantage.co/query?function=EARNINGS&symbol={symbol}&apikey={api_key}";
            var results = await APIUtilities.GetDataAsync<T>(url, cancel_token: ct);
            return results;
        }

        public async Task<T?> GetEarningsEstimate<T>(string symbol, CancellationToken ct = default)
        {
            string url = $"https://www.alphavantage.co/query?function=EARNINGS_ESTIMATES&symbol={symbol}&apikey={api_key}";
            var results = await APIUtilities.GetDataAsync<T>(url, cancel_token: ct);
            return results;
        }

        public async Task<List<T>> GetAllActiveListings<T>(CancellationToken ct = default)
        {
            string url = $"https://www.alphavantage.co/query?function=LISTING_STATUS&apikey={api_key}";
            var results = await APIUtilities.GetCsvDataAsync<T>(url, cancel_token: ct);
            return results;
        }


        public async Task<List<T>> GetAllListings<T>(DateOnly date, ListingStatus listing_status, CancellationToken ct = default)
        {
            string url = $"https://www.alphavantage.co/query?function=LISTING_STATUS&date={date.ToString("D")}&state={listing_status.ToDescriptionString()}&apikey={api_key}";
            var results = await APIUtilities.GetCsvDataAsync<T>(url, cancel_token: ct);
            return results;
        }


        public async Task<List<T>> GetEarningsCalandar<T>(string symbol, EarningsHorizon horizon, CancellationToken ct = default)
        {
            string url = $"https://www.alphavantage.co/query?function=EARNINGS_CALENDAR&symbol={symbol}&horizon={horizon.ToDescriptionString()}&&apikey={api_key}";
            var results = await APIUtilities.GetCsvDataAsync<T>(url, cancel_token: ct);
            return results;
        }


        public async Task<List<T>> GetIPOCalandar<T>(DateOnly date, CancellationToken ct = default)
        {
            string url = $"https://www.alphavantage.co/query?function=IPO_CALENDAR&apikey={api_key}";
            var results = await APIUtilities.GetCsvDataAsync<T>(url, cancel_token: ct);
            return results;
        }


    }
}
