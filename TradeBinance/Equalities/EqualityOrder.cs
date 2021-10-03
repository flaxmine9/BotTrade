using Binance.Net.Objects.Futures.FuturesData;
using System.Collections.Generic;

namespace TradeBinance.Equalities
{
    public class EqualityOrder : IEqualityComparer<BinanceFuturesOrder>
    {
        public bool Equals(BinanceFuturesOrder x, BinanceFuturesOrder y)
        {
            return x.OrderId == y.OrderId;
        }

        public int GetHashCode(BinanceFuturesOrder obj)
        {
            if (obj is null) return 0;

            int orderId = obj.OrderId.GetHashCode();

            return orderId;
        }
    }
}
