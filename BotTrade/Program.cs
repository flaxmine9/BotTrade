using DataBase;
using DataBase.Models;
using Strategies;
using System;
using System.Linq;
using TradeBinance;

namespace FlaxTrade
{
    public class Program
    {
        public static void Main(string[] args)
        {
            ApplicationContext db = new ApplicationContext();


            db.Users.Add(new DataBase.Models.User() { Name = "kirill", TelegramUserId = 0 });
            db.SaveChanges();

            using (ApplicationContext db2 = new ApplicationContext())
            {
                // получаем объекты из бд и выводим на консоль
                var users = db.Users.ToList();
                Console.WriteLine("Users list:");
                foreach (User u in users)
                {
                    Console.WriteLine($"{u.Id}.{u.Name}");
                }
            }

            Console.WriteLine("Данные сохранены!");

            //string keyKirill = "YL48f5d8ziBBC1XPZ9iOD3C7zw9utoOkPYg5Tqtxpi6ktesCFwGLxeOCj3Dxvidx";
            //string secretKeyKirill = "wzaRVq19ithkOQ2kLCV6iNgyneveJWUMihNP2OY8WrSMZIXR27IPgEIYfhSjqnuk";

            //Client client = new Client("kirill", new ApiSetting() { Key = keyKirill, SecretKey = secretKeyKirill }, db);
            //client.AddStrategy(new StrategySuperTrendSSL
            //    (
            //       new TradeSetting(takeProfit: 1.02m, stopLoss: 1.01m, leverage: 7, futuresMarginType: "Isolated",
            //            maxOrders: 10, balanceUSDT: 15.0m, maxPositions: 2)
            //    ));

            //client.StartStrategies();

            Console.ReadLine();
        }
    }
}