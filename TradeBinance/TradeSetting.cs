namespace TradeBinance
{
    public class TradeSetting
    {
        public decimal TakeProfit { get; set; }
        public decimal StopLoss { get; set; }
        public int Leverage { get; set; }
        public int MaxOrders { get; set; }
        public decimal BalanceUSDT { get; set; }
        public int MaxPositions { get; set; }
        public TimeFrame TimeFrame { get; set; }

        /// <summary>
        /// Types: Isolated, Cross
        /// </summary>
        public string FuturesMarginType { get; set; }

        public TradeSetting(TimeFrame timeFrame, decimal takeProfit, decimal stopLoss, int leverage, string futuresMarginType, int maxOrders, decimal balanceUSDT, int maxPositions)
        {
            TakeProfit = takeProfit;
            StopLoss = stopLoss;
            Leverage = leverage;
            FuturesMarginType = futuresMarginType;
            MaxOrders = maxOrders;
            BalanceUSDT = balanceUSDT;
            MaxPositions = maxPositions;
            TimeFrame = timeFrame;
        }
    }

    public enum TimeFrame
    {
        //
        // Summary:
        //     1m
        OneMinute,
        //
        // Summary:
        //     3m
        ThreeMinutes,
        //
        // Summary:
        //     5m
        FiveMinutes,
        //
        // Summary:
        //     15m
        FifteenMinutes,
        //
        // Summary:
        //     30m
        ThirtyMinutes,
        //
        // Summary:
        //     1h
        OneHour,
        //
        // Summary:
        //     2h
        TwoHour,
        //
        // Summary:
        //     4h
        FourHour,
        //
        // Summary:
        //     6h
        SixHour,
        //
        // Summary:
        //     8h
        EightHour,
        //
        // Summary:
        //     12h
        TwelveHour,
        //
        // Summary:
        //     1d
        OneDay,
        //
        // Summary:
        //     3d
        ThreeDay,
        //
        // Summary:
        //     1w
        OneWeek,
        //
        // Summary:
        //     1M
        OneMonth
    }
}
