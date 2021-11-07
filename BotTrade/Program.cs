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
            string key = "Bam9fBnYh5ZKES83ZylyHAs3nekWi22pODywKiYjRlXhTo38XQcaJk2HzrVZJPiU";
            string secretKey = "6F2LYFGc6cgLnn5fZ3gzD2ydI0xrAKA3kpvxVjOaMYXMKtN2ukk1p4TlI2NpB8QR";

            Client clientFlax = new Client("flaxmine", NetBinance.BinanceMain, new ApiSetting() { Key = key, SecretKey = secretKey }, new ApplicationContext());
            //clientFlax.AddStrategy(new StrategySuperTrendSSL
            //    (
            //       new TradeSetting(TimeFrame.FiveMinutes, takeProfit: 1.0085m, stopLoss: 1.005m, leverage: 10, futuresMarginType: "Isolated",
            //            maxOrders: 10, balanceUSDT: 8.5m, maxPositions: 1)
            //    ));
            clientFlax.AddStrategy(new Scalping
                (
                    new TradeSetting(TimeFrame.FiveMinutes, takeProfit: 1.015m, stopLoss: 1.01m, leverage: 5, futuresMarginType: "Isolated",
                        maxOrders: 6, balanceUSDT: 4.5m, maxPositions: 2)
                ));

            clientFlax.StartStrategies();

            Console.ReadLine();
        }
    }
}