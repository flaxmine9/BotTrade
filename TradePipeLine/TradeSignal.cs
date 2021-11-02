using System;
using TradeBinance.Models;

namespace TradePipeLine
{
    public class TradeSignal
    {
        public string Symbol { get; set; }
        public TypePosition TypePosition { get; set; }
        public decimal Price { get; set; }
        public DateTime CloseTime { get; set; }
    }
}
