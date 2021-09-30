using Binance.Net.Objects.Futures.FuturesData;
using System.Collections.Generic;

namespace TradeBinance.Models
{
    public class GridOrder
    {
        public List<BinanceFuturesBatchOrder> LimitOrders { get; set; }
        public List<BinanceFuturesPlacedOrder> ClosePositionOrders { get; set; }
    }
}
