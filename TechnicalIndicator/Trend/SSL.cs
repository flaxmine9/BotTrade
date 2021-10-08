using Skender.Stock.Indicators;
using System.Collections.Generic;
using System.Linq;
using TechnicalIndicator.Models;

namespace TechnicalIndicator.Trend
{
    public class SSL
    {
        public SSlValues GetSSL(IEnumerable<Kline> klines, int period)
        {
            IEnumerable<Quote> quotes = klines.Select(x => new Quote()
            {
                Close = x.Close,
                Low = x.Low,
                High= x.High,
                Open = x.Open,
                Volume = x.QuoteVolume,
                Date = x.CloseTime
            });

            List<SmaResult> highEMA = quotes.GetSma(period, CandlePart.High).Where(x => x.Sma != null).ToList();
            List<SmaResult> lowEMA = quotes.GetSma(period, CandlePart.Low).Where(x => x.Sma != null).ToList();

            List<decimal> sslUp = new List<decimal>();
            List<decimal> sslDown = new List<decimal>();

            var newKlines = klines.Skip(period - 1).ToList();
            int hlv = 0;

            for (int i = 0; i < highEMA.Count; i++)
            {
                hlv = newKlines[i].Close > highEMA[i].Sma.Value ? 1 : newKlines[i].Close < lowEMA[i].Sma.Value ? -1 : hlv;

                sslUp.Add(hlv < 0 ? lowEMA[i].Sma.Value : highEMA[i].Sma.Value);
                sslDown.Add(hlv < 0 ? highEMA[i].Sma.Value : lowEMA[i].Sma.Value);
            }

            return new SSlValues() 
            {
                Symbol = klines.First().Symbol,
                SSlDown = sslDown, 
                SSlUp = sslUp
            };
        }

        public bool CrossOverLong(SSlValues ssl)
        {
            if(ssl.SSlUp.Last() > ssl.SSlDown.Last() 
                && ssl.SSlUp.SkipLast(1).Last() < ssl.SSlDown.SkipLast(1).Last())
            {
                return true;
            }

            return false;
        }

        public bool CrossOverShort(SSlValues ssl)
        {
            if (ssl.SSlUp.Last() < ssl.SSlDown.Last()
               && ssl.SSlUp.SkipLast(1).Last() > ssl.SSlDown.SkipLast(1).Last())
            {
                return true;
            }

            return false;
        }
    }

    public class SSlValues
    {
        public string Symbol { get; set; }
        public List<decimal> SSlUp { get; set; }
        public List<decimal> SSlDown { get; set; }
    }
}
