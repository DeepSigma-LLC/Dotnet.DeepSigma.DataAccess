using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Dotnet.DeepSigma.DataAccess.API.AlphaVantage.Enums
{
    public enum TimeSeriesInterval
    {
        [Description("1Min")]
        One,
        [Description("5Min")]
        Five,
        [Description("10Min")]
        Ten,
        [Description("15Min")]
        Fifteen,
        [Description("30Min")]
        Thirty,
        [Description("60Min")]
        Sixty
    }
}
