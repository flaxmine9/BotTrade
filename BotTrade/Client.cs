using DataBase;
using Strategy.Interfaces;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace FlaxTrade
{
    public class Client
    {
        private ApiSetting _apiSetting { get; set; }
        private List<IStrategy> _strategies { get; set; }
        private ApplicationContext _dataBase { get; set; }
        private string NameUser { get; set; }

        public Client(string nameUser, ApiSetting apiSetting, ApplicationContext dataBase)
        {
            _apiSetting = apiSetting;
            NameUser = nameUser;

            _dataBase = dataBase;

            _strategies = new();
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
                _dataBase.Users.Add(new DataBase.Models.User() { Name = NameUser, TelegramUserId = 0 });
                _dataBase.SaveChanges();
            }

            foreach (IStrategy strategy in _strategies)
            {
                new Task(() => strategy.Start(NameUser, _apiSetting.Key, _apiSetting.SecretKey, _dataBase)).Start();
            }
        }
    }
}
