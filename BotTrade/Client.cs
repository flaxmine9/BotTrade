using BotTrade;
using DataBase;
using DataBase.Models;
using Strategy.Interfaces;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace FlaxTrade
{
    public class Client
    {
        private NetBinance _typeNetBinance { get; set; }
        private ApiSetting _apiSetting { get; set; }
        private List<IStrategy> _strategies { get; set; }
        private ApplicationContext _dataBase { get; set; }

        private string NameUser { get; set; }

        public Client(string nameUser, NetBinance typeNetBinance, ApiSetting apiSetting)
        {
            NameUser = nameUser;
            _apiSetting = apiSetting;

            _dataBase = new ApplicationContext();

            _strategies = new();

            _typeNetBinance = typeNetBinance;
        }

        public void AddStrategy(IStrategy strategy)
        {
            _strategies.Add(strategy);
        }

        public void StartStrategies()
        {
            var isAny = _dataBase.Users.Where(x => x.Name.Equals(NameUser)).ToList().Any();
            if (!isAny)
            {
                _dataBase.Users.Add(new User() { Name = NameUser, TelegramUserId = 0 });
                _dataBase.SaveChanges();
            }

            foreach (IStrategy strategy in _strategies)
            {
                new Task(() => strategy.Start(NameUser, _apiSetting.Key, _apiSetting.SecretKey, new ApplicationContext(), _typeNetBinance.Equals(NetBinance.BinanceMain) ? "MainNet" : "TestNet")).Start();
            }
        }
    }
}
