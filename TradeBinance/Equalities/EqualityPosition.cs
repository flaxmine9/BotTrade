using Binance.Net.Objects.Futures.FuturesData;
using System.Collections.Generic;

namespace TradeBinance.Equalities
{
    public class EqualityPosition : IEqualityComparer<BinancePositionDetailsUsdt>
    {
        public bool Equals(BinancePositionDetailsUsdt x, BinancePositionDetailsUsdt y)
        {
            return x.Symbol == y.Symbol;
        }

        public int GetHashCode(BinancePositionDetailsUsdt obj)
        {
            if (obj is null) return 0;

            int symbolHash = obj.Symbol.GetHashCode();

            return symbolHash;
        }
    }
}
