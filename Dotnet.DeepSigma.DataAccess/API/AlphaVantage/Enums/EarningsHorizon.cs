using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DeepSigma.DataAccess.API.AlphaVantage.Enums
{
    public enum EarningsHorizon
    {
        [Description("3month")]
        ThreeMonth,
        [Description("6month")]
        SixMonth,
        [Description("12month")]
        TwelveMonth
    }
}
