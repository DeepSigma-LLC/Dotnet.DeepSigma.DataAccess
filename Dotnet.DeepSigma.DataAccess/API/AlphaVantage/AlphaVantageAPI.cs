using System;
using System.Collections.Generic;
using System.Formats.Asn1;
using System.Globalization;
using System.Linq;
using System.Net;
using System.Security.Policy;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace DeepSigma.DataAccess.API.AlphaVantage
{
    public class AlphaVantageAPI
    {
        private string api_key {  get; }
        public Utilities Utilities { get; }
        public StockData StockData { get; }
        public OptionData OptionData { get; }
        public CommodityData CommodityData { get; }
        public FundamentalData FundamentalData { get; }
        public FXData FXData { get; }
        public CryptoData CryptoData { get; }
        public EconomicData EconomicData { get; }
        public AlphaVantageAPI(string api_key = "demo")
        {
            this.api_key = api_key;
            Utilities = new Utilities(api_key);
            StockData = new StockData(api_key);
            OptionData = new OptionData(api_key);
            CommodityData = new CommodityData(api_key);
            FundamentalData = new FundamentalData(api_key);
            FXData = new FXData(api_key);
            CryptoData = new CryptoData(api_key);
            EconomicData = new EconomicData(api_key);
        }




        
    }
}
