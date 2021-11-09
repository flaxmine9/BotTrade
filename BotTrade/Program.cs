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
                    new TradeSetting(TimeFrame.FiveMinutes, takeProfit: 1.035m, stopLoss: 1.02m, leverage: 5, futuresMarginType: "Isolated",
                        maxOrders: 10, balanceUSDT: 7.0m, maxPositions: 1)
                ));

            clientFlax.StartStrategies();


            Client clientKirill = new Client("kirill", NetBinance.BinanceMain, new ApiSetting()
            {
                Key = BinanceKey.KeyKirill,
                SecretKey = BinanceKey.KeySecretKirill
            }, new ApplicationContext());

            clientKirill.AddStrategy(new Butenko
                (
                    new TradeSetting(TimeFrame.FiveMinutes, takeProfit: 1.03m, stopLoss: 1.015m, leverage: 7, futuresMarginType: "Isolated",
                        maxOrders: 15, balanceUSDT: 12.0m, maxPositions: 2)
                ));

            clientKirill.StartStrategies();

            Console.ReadLine();
        }
    }
}