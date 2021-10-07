using Strategy.Interfaces;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TechnicalIndicator.Models;
using TradeBinance;
using System.Threading.Tasks.Dataflow;
using TechnicalIndicator.Trend;

namespace Strategies
{
    public class SuperTrendHeikin : IStrategy
    {
        private TradeSetting _tradeSetting { get; set; }
        private Trade _trade { get; set; }

        private SuperTrend _superTtrend {  get; set; }
        private SmoothedHeikinAshi _smoothedHeikin { get; set; }

        private BufferBlock<Signal> _bufferSignals { get; set; }

        public SuperTrendHeikin(TradeSetting tradeSetting)
        {
            _tradeSetting = tradeSetting;

            _bufferSignals = new BufferBlock<Signal>();
            _superTtrend = new SuperTrend();
            _smoothedHeikin = new SmoothedHeikinAshi(20, 10);
        }

        public async Task Logic()
        {
            await Task.Delay(1);
        }

        public async Task Start(string key, string secretKey)
        {
            _trade = new Trade(key, secretKey, _tradeSetting);

            
        }
    }
}
