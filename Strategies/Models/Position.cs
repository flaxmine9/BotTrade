using TradeBinance.Models;

namespace Strategies.Models
{
    public class Position
    {
        public string Symbol { get; set; }
        public TypePosition TypePosition { get; set; }
        public decimal Price { get; set; }
    }
}
