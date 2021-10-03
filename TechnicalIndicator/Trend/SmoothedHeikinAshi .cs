using Skender.Stock.Indicators;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TechnicalIndicator.Models;

namespace TechnicalIndicator.Trend
{
    public class SmoothedHeikinAshi
    {

        private int _firstSmoothPeriod { get;set; }
        private int _secondSmoothPeriod {  get;set; }

        public SmoothedHeikinAshi(int firstSmootPeriod, int secondSmoothPeriod)
        {
            _firstSmoothPeriod = firstSmootPeriod;
            _secondSmoothPeriod = secondSmoothPeriod;
        }

        public Kline GetSmoothedHeikinAshi(IEnumerable<Kline> klines)
        {
            IEnumerable<Quote> quotes = klines.Select(x => new Quote()
            {
                Close = x.Close,
                Low = x.Low,
                High = x.High,
                Open = x.Open
            });

            IEnumerable<HeikinAshiResult> heikin = quotes.GetHeikinAshi();

            #region smooth heikin fast sma

            var fastSMAOpen = heikin.Select(x => new Quote()
            {
                Open = x.Open
            }).GetEma(_firstSmoothPeriod, CandlePart.Open).Where(x => x.Ema != null);

            var fastSMAClose = heikin.Select(x => new Quote()
            {
                Close = x.Close
            }).GetEma(_firstSmoothPeriod, CandlePart.Close).Where(x => x.Ema != null);


            var fastSMAHigh = heikin.Select(x => new Quote()
            {
                High = x.High
            }).GetEma(_firstSmoothPeriod, CandlePart.High).Where(x => x.Ema != null);

            var fastSMALow = heikin.Select(x => new Quote()
            {
                Low = x.Low
            }).GetEma(_firstSmoothPeriod, CandlePart.Low).Where(x => x.Ema != null);

            #endregion

            #region smooth heikin low sma

            var lowSMAClose = fastSMAClose.Select(x => x.Ema).Select(x => new Quote()
            {
                Close = x.Value
            }).GetEma(_secondSmoothPeriod, CandlePart.Close).Where(x => x.Ema != null);

            var lowSMAOpen = fastSMAOpen.Select(x => x.Ema).Select(x => new Quote()
            {
                Open = x.Value
            }).GetEma(_secondSmoothPeriod, CandlePart.Open).Where(x => x.Ema != null);

            var lowSMAHigh = fastSMAHigh.Select(x => x.Ema).Select(x => new Quote()
            {
                High = x.Value
            }).GetEma(_secondSmoothPeriod, CandlePart.High).Where(x => x.Ema != null);

            var lowSMALow = fastSMALow.Select(x => x.Ema).Select(x => new Quote()
            {
                Low = x.Value
            }).GetEma(_secondSmoothPeriod, CandlePart.Low).Where(x => x.Ema != null);

            #endregion

            return new Kline()
            {
                Close = lowSMAClose.Last().Ema.Value,
                Open = lowSMAOpen.Last().Ema.Value,
                High = lowSMAHigh.Last().Ema.Value,
                Low = lowSMALow.Last().Ema.Value,
            };
        }
    }
}
