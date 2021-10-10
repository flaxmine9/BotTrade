using Strategies;
using System;
using TradeBinance;

namespace FlaxTrade
{
    public class Program
    {
        public static void Main(string[] args)
        {
            //string keyTest = "581d213f387a76f1280c9072d6df0112d519dbd05752e3175feb60b80726c0a5";
            //string secretKeyTest = "69ad2ca09dfb9f21a325e1430feb5b423c763a8aa497071ca341b01adf693df2";

            string keyKirill = "YL48f5d8ziBBC1XPZ9iOD3C7zw9utoOkPYg5Tqtxpi6ktesCFwGLxeOCj3Dxvidx";
            string secretKeyKirill = "wzaRVq19ithkOQ2kLCV6iNgyneveJWUMihNP2OY8WrSMZIXR27IPgEIYfhSjqnuk";

            Client clientKirill = new Client(new ApiSetting() { Key = keyKirill, SecretKey = secretKeyKirill });
            clientKirill.AddStrategy(new StrategySuperTrendSSL
                (
                   new TradeSetting(takeProfit: 1.03m, stopLoss: 1.02m, leverage: 5, futuresMarginType: "Isolated", maxOrders: 10, balanceUSDT: 20.0m)
                ));

            clientKirill.StartStrategies();


            Console.ReadLine();
        }
    }
}