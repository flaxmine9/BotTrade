using Skender.Stock.Indicators;
using System.Collections.Generic;
using System.Linq;
using TechnicalIndicator.Models;

namespace TechnicalIndicator.Trend
{
    public class ROC
    {
        public IEnumerable<RocResult> GetRoc(IEnumerable<Kline> klines, int period)
        {
            IEnumerable<Quote> quotes = klines.Select(x => new Quote()
            {
                Close = x.Close,
                Low = x.Low,
                High = x.High,
                Open = x.Open,
                Date = x.CloseTime,
                Volume = x.QuoteVolume
            });

            return quotes.GetRoc(period);
        }
    }
}
