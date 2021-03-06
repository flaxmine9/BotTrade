using System;

namespace TechnicalIndicator.Models
{
    public class Kline
    {
        public string Symbol { get; set; }
        public decimal Close { get; set; }
        public decimal Open { get; set; }
        public decimal Low { get; set; }
        public decimal High { get; set; }
        public decimal BaseVolume { get; set; }
        public decimal QuoteVolume { get; set; }
        public int TradeCount { get; set; }
        public decimal TakerBuyBaseVolume { get; set; }
        public decimal TakerBuyQuoteVolume { get; set; }

        public DateTime CloseTime { get; set; }
        public DateTime OpenTime { get; set; }
    }
}
