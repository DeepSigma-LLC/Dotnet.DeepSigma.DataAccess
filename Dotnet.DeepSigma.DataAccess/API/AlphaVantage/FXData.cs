using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Dotnet.DeepSigma.DataAccess.API.AlphaVantage
{
    public class FXData
    {
        private string api_key { get; }

        public FXData(string api_key)
        {
            this.api_key = api_key;
        }
    }
}
