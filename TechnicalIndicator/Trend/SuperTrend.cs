using Skender.Stock.Indicators;
using System.Collections.Generic;
using System.Linq;
using TechnicalIndicator.Models;

namespace TechnicalIndicator.Trend
{
    public class SuperTrend
    {
        public IEnumerable<SuperTrendResult> GetSuperTrend(IEnumerable<Kline> klines, int period, decimal multiplier = 3.0m)
        {
            IEnumerable<Quote> quotes = klines.Select(x => new Quote()
            { 
                Close= x.Close,
                Low= x.Low,
                High= x.High,
                Open= x.Open,
                Date = x.CloseTime,
                Volume = x.QuoteVolume
            });

            return quotes.GetSuperTrend(period, multiplier);
        }
    }
}
