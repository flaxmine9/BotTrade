using Skender.Stock.Indicators;
using System.Collections.Generic;
using System.Linq;
using TechnicalIndicator.Models;

namespace TechnicalIndicator.Trend
{
    public class SuperTrend
    {
        public SuperTrend()
        {

        }

        public IEnumerable<SuperTrendResult> GetSuperTrend(IEnumerable<Kline> klines)
        {
            IEnumerable<Quote> quotes = klines.Select(x => new Quote()
            { 
                Close= x.Close,
                Low= x.Low,
                High= x.High,
                Open= x.Open
            });

            return quotes.GetSuperTrend(25, 3);
        }
    }
}
