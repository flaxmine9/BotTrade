namespace TradeBinance
{
    public class TradeSetting
    {
        public decimal TakeProfit { get; set; }
        public decimal StopLoss { get; set; }
        public int Leverage { get; set; }

        /// <summary>
        /// Types: Isolated, Cross
        /// </summary>
        public string FuturesMarginType { get; set; }

        public TradeSetting(decimal takeProfit, decimal stopLoss, int leverage, string futuresMarginType)
        {
            TakeProfit = takeProfit;
            StopLoss = stopLoss;
            Leverage = leverage;
            FuturesMarginType = futuresMarginType;
        }
    }
}
