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
            await _trade.SetExchangeInformationAsync();

            var gridOrder = new TransformBlock<BinancePositionDetailsUsdt, GridOrder>(position =>
            {
                return _trade.GetGridOrders(position); 
            });

            var createOrders = new TransformBlock<GridOrder, string>(async order =>
            {
                await _trade.PlaceOrders(order);

                return order.ClosePositionOrders.First().Symbol;
            }, new ExecutionDataflowBlockOptions
            {
                MaxDegreeOfParallelism = DataflowBlockOptions.Unbounded
            });

            var controlOrders = new ActionBlock<string>(async symbol =>
            {
                await _trade.ControlOrders(symbol);
            }, new ExecutionDataflowBlockOptions { MaxDegreeOfParallelism = DataflowBlockOptions.Unbounded });

            var linkOptions = new DataflowLinkOptions { PropagateCompletion = true };

            bufferPositions.LinkTo(gridOrder, linkOptions);
            gridOrder.LinkTo(createOrders, linkOptions);
            createOrders.LinkTo(controlOrders, linkOptions);


            var positions = ProducePosition();

            await positions;
        }

        public async Task Start(string key, string secretKey)
        {
            _trade = new Trade(key, secretKey, _tradeSetting);

            await Logic();
        }

        public async Task ProducePosition()
        {
            for (uint i = 0; i < uint.MaxValue; i++)
            {
                IEnumerable<BinancePositionDetailsUsdt> positions = await _trade.GetCurrentOpenPositionsAsync();
                if (!positions.Any())
                {
                    currentOpenPositions = new List<BinancePositionDetailsUsdt>();
                }

                var exceptedPositions = positions.Except(currentOpenPositions, comparer: equalityPosition);
                if (exceptedPositions.Any())
                {
                    foreach (var position in exceptedPositions)
                    {
                        var openOrders = await _trade.GetOpenOrders(position.Symbol);
                        if (!openOrders.Any())
                        {
                            await bufferPositions.SendAsync(position);
                            currentOpenPositions.Add(position);
                        }
                    }
                }
                await Task.Delay(1500);
            }
        }
    }
}
