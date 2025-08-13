using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace Dotnet.DeepSigma.DataAccess.API.AlphaVantage.DataModels
{
    public sealed class AlphaVantageBar
    {
        [JsonPropertyName("1. open")] public decimal Open { get; init; }
        [JsonPropertyName("2. high")] public decimal High { get; init; }
        [JsonPropertyName("3. low")] public decimal Low { get; init; }
        [JsonPropertyName("4. close")] public decimal Close { get; init; }
        [JsonPropertyName("5. volume")] public long Volume { get; init; }
    }


}
