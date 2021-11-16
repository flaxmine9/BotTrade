﻿using Skender.Stock.Indicators;
using System.Collections.Generic;
using System.Linq;
using TechnicalIndicator.Models;

namespace TechnicalIndicator.Trend
{
    public class SMA
    {
        public IEnumerable<SmaResult> GetEma(IEnumerable<Kline> klines, int period)
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

            return quotes.GetSma(period);
        }
    }
}
