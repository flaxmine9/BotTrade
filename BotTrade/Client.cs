using Strategy.Interfaces;
using System.Collections.Generic;

namespace FlaxTrade
{
    public class Client
    {
        private ApiSetting _apiSetting { get; set; }
        private List<IStrategy> _strategies { get; set; }

        public Client(ApiSetting apiSetting)
        {
            _apiSetting = apiSetting;
            _strategies = new List<IStrategy>();
        }

        public void AddStrategy(IStrategy strategy)
        {
            _strategies.Add(strategy);
        }

        public void StartStrategies()
        {
            foreach (IStrategy strategy in _strategies)
            {
                new Task(() => strategy.Start(_apiSetting.Key, _apiSetting.SecretKey)).Start();
            }
        }
    }
}
