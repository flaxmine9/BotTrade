using BotTrade;
using DataBase;
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

            clientFlax.AddStrategy(new Butenko
                (
                    new TradeSetting(TimeFrame.FiveMinutes, takeProfit: 1.03m, stopLoss: 1.01m, leverage: 7, futuresMarginType: "Isolated",
                        maxOrders: 10, balanceUSDT: 8.5m, maxPositions: 1)
                ));

            clientFlax.AddStrategy(new Scalping
                (
                    new TradeSetting(TimeFrame.FiveMinutes, takeProfit: 1.035m, stopLoss: 1.02m, leverage: 5, futuresMarginType: "Isolated",
                        maxOrders: 5, balanceUSDT: 7.5m, maxPositions: 1)
                ));

            clientFlax.StartStrategies();

            Console.ReadLine();
        }
    }
}