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
            ApplicationContext db = new ApplicationContext();

            string keyKirill = "YL48f5d8ziBBC1XPZ9iOD3C7zw9utoOkPYg5Tqtxpi6ktesCFwGLxeOCj3Dxvidx";
            string secretKeyKirill = "wzaRVq19ithkOQ2kLCV6iNgyneveJWUMihNP2OY8WrSMZIXR27IPgEIYfhSjqnuk";

            Client client = new Client("kirill", new ApiSetting() { Key = keyKirill, SecretKey = secretKeyKirill }, db);
            client.AddStrategy(new StrategySuperTrendSSL
                (
                   new TradeSetting(TimeFrame.FiveMinutes, takeProfit: 1.02m, stopLoss: 1.01m, leverage: 7, futuresMarginType: "Isolated",
                        maxOrders: 10, balanceUSDT: 15.0m, maxPositions: 2)
                ));

            client.StartStrategies();

            Console.ReadLine();
        }
    }
}