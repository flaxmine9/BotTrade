using System;
using System.Collections.Generic;

namespace DataBase.Models
{
    public class Order
    {
        public int Id {  get; set; }
        public int UserId { get; set; }
        public string Symbol { get; set; }
        public string NameStrategy {  get; set; }
        public long OrderId { get; set; }
        public decimal Price { get; set; }
        public decimal Quantity { get; set; }
        public decimal Commission { get; set; }
        public decimal RealizedPnl { get; set; }
        public string Side { get; set; }
        public string PositionSide { get; set; }
        public DateTime TradeTime { get; set; }
    }
}
