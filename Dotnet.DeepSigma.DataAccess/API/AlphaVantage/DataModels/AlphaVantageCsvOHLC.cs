using CsvHelper.Configuration.Attributes;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DeepSigma.DataAccess.API.AlphaVantage.DataModels
{
    internal class AlphaVantageCsvOHLC
    {
        [Name("timestamp")]
        public DateTime Timestamp { get; init; }

        [Name("open")] 
        public decimal Open { get; init; }

        [Name("high")]
        public decimal High { get; init; }

        [Name("low")]
        public decimal Low { get; init; }

        [Name("close")]
        public decimal Close { get; init; 
        }
        [Name("volume")]
        public long Volume { get; init; }
    }
}
