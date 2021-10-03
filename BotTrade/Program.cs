using Strategies;
using Strategy;
using System;
using System.Collections.Generic;
using TechnicalIndicator.Pivot;
using TechnicalIndicator.Pivot.PivotTypes;
using TradeBinance;

namespace FlaxTrade
{
    public class Program
    {
        public static void Main(string[] args)
        {
            string key = "Bam9fBnYh5ZKES83ZylyHAs3nekWi22pODywKiYjRlXhTo38XQcaJk2HzrVZJPiU";
            string secretKey = "6F2LYFGc6cgLnn5fZ3gzD2ydI0xrAKA3kpvxVjOaMYXMKtN2ukk1p4TlI2NpB8QR";

            //string keyTest = "581d213f387a76f1280c9072d6df0112d519dbd05752e3175feb60b80726c0a5";
            //string secretKeyTest = "69ad2ca09dfb9f21a325e1430feb5b423c763a8aa497071ca341b01adf693df2";

            //string keyTestKirill = "2929b75ae210c87f385e2bf0bd8f62bfa4a16da303272fb9bd765876a4a484a7";
            //string secretKeyTestKirill = "243ed78641c70fd7a7336a05d26cde385ec8f1d3ce1320aa8bfd1bbe4bb96f7b";


            Client client = new Client(new ApiSetting() { Key = key, SecretKey = secretKey });
            client.AddStrategy(new StrategyCPR
                (
                   new TradeSetting(takeProfit: 1.02m, stopLoss: 1.015m, leverage: 5, futuresMarginType: "Isolated", maxOrders: 5),
                   new PivotPoint(new Traditional())

                ));

            client.StartStrategies();


            //Client client = new Client(new ApiSetting() { Key = key, SecretKey = secretKey });
            //client.AddStrategy(new SuperTrendHeikin
            //    (
            //       new TradeSetting(takeProfit: 1.02m, stopLoss: 1.015m, leverage: 5, futuresMarginType: "Isolated")
            //    ));

            //client.StartStrategies();

            Console.ReadLine();
        }
    }
}