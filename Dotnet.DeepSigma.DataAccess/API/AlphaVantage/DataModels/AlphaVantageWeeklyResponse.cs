using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace DeepSigma.DataAccess.API.AlphaVantage.DataModels
{
    internal sealed class AlphaVantageWeeklyResponse
    {
        [JsonPropertyName("Meta Data")] public AlphaVantageMeta Meta { get; init; } = new();
            // Keys are dates like "2025-08-12"; we'll keep them as strings or post-convert.
        [JsonPropertyName("Weekly Time Series")] public Dictionary<string, AlphaVantageBar> Series { get; init; } = new();
    }
}
