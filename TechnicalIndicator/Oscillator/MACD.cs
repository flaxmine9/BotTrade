using Skender.Stock.Indicators;
using System.Collections.Generic;
using System.Linq;
using TechnicalIndicator.Models;

namespace TechnicalIndicator.Oscillator
{
    public class MACD
    {
        public IEnumerable<MacdResult> GetMACD(IEnumerable<Kline> klines, int fastPeriod = 12, int slowPeriod = 26, int signalPeriod = 9)
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

            return quotes.GetMacd(fastPeriod, slowPeriod, signalPeriod);
        }
    }
}
