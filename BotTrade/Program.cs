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
            string keyKirill = "YL48f5d8ziBBC1XPZ9iOD3C7zw9utoOkPYg5Tqtxpi6ktesCFwGLxeOCj3Dxvidx";
            string secretKeyKirill = "wzaRVq19ithkOQ2kLCV6iNgyneveJWUMihNP2OY8WrSMZIXR27IPgEIYfhSjqnuk";

            string key = "Bam9fBnYh5ZKES83ZylyHAs3nekWi22pODywKiYjRlXhTo38XQcaJk2HzrVZJPiU";
            string secretKey = "6F2LYFGc6cgLnn5fZ3gzD2ydI0xrAKA3kpvxVjOaMYXMKtN2ukk1p4TlI2NpB8QR";

            Client client = new Client("kirill", new ApiSetting() { Key = keyKirill, SecretKey = secretKeyKirill }, new ApplicationContext());
            client.AddStrategy(new StrategySuperTrendSSL
                (
                   new TradeSetting(TimeFrame.FiveMinutes, takeProfit: 1.02m, stopLoss: 1.01m, leverage: 7, futuresMarginType: "Isolated",
                        maxOrders: 10, balanceUSDT: 15.0m, maxPositions: 2)
                ));

            client.StartStrategies();

            Client clientFlax = new Client("flaxmine", new ApiSetting() { Key = key, SecretKey = secretKey }, new ApplicationContext());
            clientFlax.AddStrategy(new StrategySuperTrendSSL
                (
                   new TradeSetting(TimeFrame.FiveMinutes, takeProfit: 1.0085m, stopLoss: 1.005m, leverage: 10, futuresMarginType: "Isolated",
                        maxOrders: 10, balanceUSDT: 8.5m, maxPositions: 1)
                ));

            clientFlax.StartStrategies();

            Console.ReadLine();
        }
    }
}