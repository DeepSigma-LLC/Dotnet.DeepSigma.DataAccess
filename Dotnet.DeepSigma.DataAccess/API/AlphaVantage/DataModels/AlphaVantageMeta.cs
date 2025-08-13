using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace Dotnet.DeepSigma.DataAccess.API.AlphaVantage.DataModels
{
    internal sealed class AlphaVantageMeta
    {
        [JsonPropertyName("1. Information")] public string Information { get; init; } = "";
        [JsonPropertyName("2. Symbol")] public string Symbol { get; init; } = "";
        [JsonPropertyName("3. Last Refreshed")] public string LastRefreshed { get; init; } = "";
        [JsonPropertyName("4. Time Zone")] public string TimeZone { get; init; } = "";
    }

}
