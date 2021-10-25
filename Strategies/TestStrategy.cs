using DataBase;
using Strategy.Interfaces;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TradeBinance;

namespace Strategies
{
    public class TestStrategy : IStrategy
    {
        private TradeSetting _tradeSetting { get; set; }
        private Trade _trade { get; set; }

        public TestStrategy(TradeSetting tradeSetting)
        {
            _tradeSetting = tradeSetting;
        }


        public async Task Logic()
        {
            
        }

        public async Task Start(string nameUser, string key, string secretKey, ApplicationContext dataBase)
        {
            _trade = new Trade(key, secretKey, _tradeSetting);

            await _trade.SetExchangeInformationAsync();

            var position = await _trade.GetCurrentOpenPositionAsync("ETHUSDT");
            var orders = _trade.GetGridOrders(position);

            var placedOrders = await _trade.PlaceOrders(orders);

            Console.WriteLine();
        }
    }
}
