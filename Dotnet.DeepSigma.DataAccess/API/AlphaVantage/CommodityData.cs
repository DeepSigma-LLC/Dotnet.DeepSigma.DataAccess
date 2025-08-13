using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Dotnet.DeepSigma.DataAccess.API.AlphaVantage
{
    public class CommodityData
    {

        private string api_key { get; }

        public CommodityData(string api_key)
        {
            this.api_key = api_key;
        }
    }
}
