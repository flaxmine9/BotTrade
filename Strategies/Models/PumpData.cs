namespace Strategies.Models
{
    public class PumpData
    {
        public string Symbol { get; set; }
        public decimal LimitVolume { get; set; }
        public decimal TakeProfit { get; set; }
        public decimal StopLoss { get; set; }
    }
}
