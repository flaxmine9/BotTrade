namespace TradeBinance
{
    public class TradeSetting
    {
        public decimal TakeProfit { get; set; }
        public decimal StopLoss { get; set; }
        public int Leverage { get; set; }
        public int MaxOrders { get; set; }
        public decimal BalanceUSDT { get; set; }

        /// <summary>
        /// Types: Isolated, Cross
        /// </summary>
        public string FuturesMarginType { get; set; }

        public TradeSetting(decimal takeProfit, decimal stopLoss, int leverage, string futuresMarginType, int maxOrders, decimal balanceUSDT)
        {
            TakeProfit = takeProfit;
            StopLoss = stopLoss;
            Leverage = leverage;
            FuturesMarginType = futuresMarginType;
            MaxOrders = maxOrders;
            BalanceUSDT = balanceUSDT;
        }
    }
}
