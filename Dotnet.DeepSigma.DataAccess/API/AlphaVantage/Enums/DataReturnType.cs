using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Dotnet.DeepSigma.DataAccess.API.AlphaVantage.Enums
{
    public enum DataReturnType
    {
        [Description("json")]
        Json,
        [Description("csv")]
        CSV
    }
}
