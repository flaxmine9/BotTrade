using BotTrade;
using Strategies;
using System;
using TradeBinance;

namespace FlaxTrade
{
    public class Program
    {
        public static void Main(string[] args)
        {
            Client clientFlax = new Client("flaxmine", NetBinance.BinanceMain, new ApiSetting()
            {
                Key = BinanceKey.Key,
                SecretKey = BinanceKey.SecretKey
            });

            clientFlax.AddStrategy(new ScalpingByTrend
                (
                    new TradeSetting(TimeFrame.FiveMinutes, takeProfit: 1.03m, stopLoss: 1.006m, leverage: 5, futuresMarginType: "Isolated",
                        maxOrders: 10, balanceUSDT: 10.5m, maxPositions: 1)
                ));

            clientFlax.StartStrategies();

            Console.ReadLine();
        }
    }
}