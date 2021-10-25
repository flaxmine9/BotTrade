using Strategy.Interfaces;
using TechnicalIndicator.Pivot;
using TradeBinance;
using System.Threading.Tasks.Dataflow;
using TradeBinance.Models;
using TradeBinance.Equalities;
using Binance.Net.Objects.Futures.FuturesData;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Linq;
using DataBase;

namespace Strategy
{
    public class StrategyCPR : IStrategy
    {
        private TradeSetting _tradeSetting { get; set; }
        private PivotPoint _pivotPoint { get; set; }
        private Trade _trade { get; set; }

        private BufferBlock<BinancePositionDetailsUsdt> bufferPositions { get; set; }
        private List<BinancePositionDetailsUsdt> currentOpenPositions { get; set; }

        private EqualityPosition equalityPosition { get; set; }

        public StrategyCPR(TradeSetting tradeSetting, PivotPoint pivotPoint)
        {
            _tradeSetting = tradeSetting;
            _pivotPoint = pivotPoint;

            bufferPositions = new BufferBlock<BinancePositionDetailsUsdt>(new DataflowBlockOptions { BoundedCapacity = 5 });
            currentOpenPositions = new List<BinancePositionDetailsUsdt>();

            equalityPosition = new EqualityPosition();
        }

        public async Task Logic()
        {
            await Task.Delay(1);
        }

        public async Task Start(string nameUser, string key, string secretKey, ApplicationContext dataBase)
        {
            _trade = new Trade(key, secretKey, _tradeSetting);

            await Logic();
        }
    }
}
