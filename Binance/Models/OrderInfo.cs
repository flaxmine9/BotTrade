using System.Collections.Generic;

namespace Binance.Models
{
    public class OrderInfo
    {
        public int QuantityOrders { get; set; }
        public List<decimal> QuantitiesAsset { get; set; }
    }
}
