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
            }, new ApplicationContext());

            clientFlax.AddStrategy(new Scalping
                (
                    new TradeSetting(TimeFrame.FiveMinutes, takeProfit: 1.015m, stopLoss: 1.01m, leverage: 5, futuresMarginType: "Isolated",
                        maxOrders: 10, balanceUSDT: 8.0m, maxPositions: 1)
                ));

            clientFlax.StartStrategies();

            Console.ReadLine();
        }
    }
}