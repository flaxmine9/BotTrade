using Skender.Stock.Indicators;
using Strategies.Models;
using Strategy.Interfaces;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TechnicalIndicator.Models;
using TechnicalIndicator.Trend;
using TradeBinance;
using TradeBinance.Models;

namespace Strategies
{
    public class StrategySuperTrendSSL : IStrategy
    {
        private Trade _trade { get; set; }
        private TradeSetting _tradeSetting { get; set; }
        private SSL _ssl { get; set; }
        private SuperTrend _superTrend { get; set; }

        private List<SuperTrendSSLData> _superTrendSSLData { get; set; }

        public StrategySuperTrendSSL(TradeSetting tradeSetting)
        {
            _tradeSetting = tradeSetting;

            _ssl = new SSL();
            _superTrend = new SuperTrend();

            _superTrendSSLData = new List<SuperTrendSSLData>()
            {
                new SuperTrendSSLData() { Symbol = "BTCUSDT", Period = 5, ATRMultiplier = 1.0m, ATRPeriod = 5 }
            };
        }

        public Task Logic()
        {
            throw new NotImplementedException();
        }

        public async Task Start(string key, string secretKey)
        {
            _trade = new Trade(key, secretKey, _tradeSetting);

            int periodKlines = 200;

            for (uint i = 0; i < uint.MaxValue; i++)
            {
                var klines = await _trade.GetLstKlinesAsync(_superTrendSSLData.Select(x => x.Symbol), periodKlines);
                Position signal = GetSignal(klines);

                if (signal != null)
                {
                    bool entriedMarket = await _trade.EntryMarket(signal.Symbol, price: signal.Price, _tradeSetting.BalanceUSDT, signal.TypePosition);
                    if (entriedMarket)
                    {
                        Console.WriteLine($"Зашли в позицию по валюте: {signal.Symbol}\n" +
                            $"Позиция: {signal.TypePosition.ToString()}");
                        var position = await _trade.GetCurrentOpenPositionAsync(signal.Symbol);
                        if (position != null)
                        {
                            var gridOrders = _trade.GetGridOrders(position);
                            await _trade.PlaceOrders(gridOrders);

                            Console.WriteLine("Поставили ордера по валюте {0}", signal.Symbol);

                            await _trade.ControlOrders(signal.Symbol);
                        }
                    }
                }
                await Task.Delay((60 - DateTime.Now.Second + 1) * 1000);
            }
        }

        private Position GetSignal(IEnumerable<IEnumerable<Kline>> klines)
        {
            foreach (IEnumerable<Kline> lstKlines in klines)
            {
                SuperTrendSSLData data = _superTrendSSLData.Where(x => lstKlines.First().Symbol.Equals(x.Symbol)).First();

                SSlValues ssl = _ssl.GetSSL(lstKlines.SkipLast(1), data.Period);
                SuperTrendResult superTrend = _superTrend.GetSuperTrend(lstKlines.SkipLast(1), data.ATRPeriod, data.ATRMultiplier).Last();

                if (superTrend.LowerBand != null && lstKlines.Last().Close > superTrend.LowerBand && _ssl.CrossOverLong(ssl))
                {
                    Console.WriteLine($"Лонг перечесение\n" +
                        $"Время: {DateTime.Now}");

                    return new Position()
                    {
                        Price = lstKlines.Last().Close,
                        Symbol = lstKlines.Last().Symbol,
                        TypePosition = lstKlines.Last().Close > lstKlines.Last().Open ? TypePosition.Long : TypePosition.Short
                    };
                }
                else if (superTrend.UpperBand != null && lstKlines.Last().Close < superTrend.UpperBand && _ssl.CrossOverShort(ssl))
                {
                    Console.WriteLine($"Шорт перечесение\n" +
                        $"Время: {DateTime.Now}");

                    return new Position()
                    {
                        Price = lstKlines.Last().Close,
                        Symbol = lstKlines.Last().Symbol,
                        TypePosition = lstKlines.Last().Close > lstKlines.Last().Open ? TypePosition.Long : TypePosition.Short
                    };
                }
            }

            return null;
        }
    }
}
