using Binance.Net.Enums;
using Binance.Net.Objects.Futures.FuturesData;
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
                new SuperTrendSSLData() { Symbol = "ETHUSDT", Period = 60, ATRMultiplier = 2.4m, ATRPeriod = 17 },
                new SuperTrendSSLData() { Symbol = "BNBUSDT", Period = 75, ATRMultiplier = 4.2m, ATRPeriod = 10 },
                new SuperTrendSSLData() { Symbol = "DOTUSDT", Period = 50, ATRMultiplier = 6.4m, ATRPeriod = 22 },
                new SuperTrendSSLData() { Symbol = "XRPUSDT", Period = 70, ATRMultiplier = 5.0m, ATRPeriod = 10 },
                new SuperTrendSSLData() { Symbol = "UNIUSDT", Period = 100, ATRMultiplier = 6.6m, ATRPeriod = 10 },
                new SuperTrendSSLData() { Symbol = "LTCUSDT", Period = 60, ATRMultiplier = 4.2m, ATRPeriod = 13 }
            };
        }

        public async Task Logic()
        {
            Console.WriteLine("Strategy is started!");

            int periodKlines = 200;

            for (uint i = 0; i < uint.MaxValue; i++)
            {
                try
                {
                    var klines = await _trade.GetLstKlinesAsync(_superTrendSSLData.Select(x => x.Symbol), KlineInterval.FiveMinutes, periodKlines);
                    Position signal = GetSignal(klines);

                    if (signal != null)
                    {
                        var balanceUSDT = await _trade.GetBalanceAsync();
                        if (balanceUSDT <= 19.9m)
                        {
                            Console.WriteLine("Баланс меньше 20$\n" +
                                $"Время: {DateTime.Now.ToUniversalTime()}");
                            break;
                        }

                        bool entriedMarket = await _trade.EntryMarket(signal.Symbol, price: signal.Price, _tradeSetting.BalanceUSDT, signal.TypePosition);
                        if (entriedMarket)
                        {
                            Console.WriteLine($"Зашли в позицию по валюте: {signal.Symbol}\n" +
                                $"Позиция: {signal.TypePosition}");
                            var position = await _trade.GetCurrentOpenPositionAsync(signal.Symbol);
                            if (position != null)
                            {
                                GridOrder gridOrders = _trade.GetGridOrders(position);
                                IEnumerable<BinanceFuturesPlacedOrder> placedOrders = await _trade.PlaceOrders(gridOrders);
                                if (placedOrders.Any())
                                {
                                    Console.WriteLine("Поставили ордера по валюте {0}", signal.Symbol);

                                    await _trade.ControlOrders(placedOrders, signal.Symbol, 1000);   
                                }
                                else
                                {
                                    Console.WriteLine($"Валюта: {signal.Symbol} -- не удалось поставить ордера, пытаем поставить заново!");
                                    placedOrders = await _trade.PlaceOrders(gridOrders);

                                    Console.WriteLine("Поставили ордера по валюте {0}", signal.Symbol);

                                    await _trade.ControlOrders(placedOrders, signal.Symbol, 2000);
                                }
                            }
                        }
                    }

                    await WaitTime();
                }
                catch (Exception ex)
                {
                    Console.WriteLine(ex.Message);
                }
            }

        }

        private async Task WaitTime()
        {
            var klineForTime = await _trade.GetKlineAsync(_superTrendSSLData.First().Symbol, KlineInterval.FiveMinutes, limit: 1);
            if(klineForTime != null)
            {
                DateTime timeNow = DateTime.Now.ToUniversalTime();
                TimeSpan waitTime = klineForTime.CloseTime.AddMilliseconds(1100) - timeNow;

                await Task.Delay((int)waitTime.TotalMilliseconds);
            }
        }

        public async Task Start(string key, string secretKey)
        {
            _trade = new Trade(key, secretKey, _tradeSetting);

            await _trade.SetExchangeInformationAsync();
            await _trade.SetTradeSettings(_superTrendSSLData.Select(x => x.Symbol));

            await Logic();
        }

        private Position GetSignal(IEnumerable<IEnumerable<Kline>> klines)
        {
            foreach (IEnumerable<Kline> lstKlines in klines)
            {
                List<Kline> withOutLastKline = lstKlines.SkipLast(1).ToList();

                SuperTrendSSLData data = _superTrendSSLData.Where(x => withOutLastKline.First().Symbol.Equals(x.Symbol)).First();

                SSlValues ssl = _ssl.GetSSL(withOutLastKline, data.Period);
                SuperTrendResult superTrend = _superTrend.GetSuperTrend(withOutLastKline, data.ATRPeriod, data.ATRMultiplier).Last();

                if (superTrend.LowerBand != null && withOutLastKline.Last().Close > superTrend.LowerBand && _ssl.CrossOverLong(ssl))
                {
                    Console.WriteLine($"Лонг перечесение\n" +
                        $"Время: {DateTime.Now}");

                    return new Position()
                    {
                        Price = withOutLastKline.Last().Close,
                        Symbol = withOutLastKline.Last().Symbol,
                        TypePosition = withOutLastKline.Last().Close > withOutLastKline.Last().Open ? TypePosition.Long : TypePosition.Short
                    };
                }
                else if (superTrend.UpperBand != null && withOutLastKline.Last().Close < superTrend.UpperBand && _ssl.CrossOverShort(ssl))
                {
                    Console.WriteLine($"Шорт перечесение\n" +
                        $"Время: {DateTime.Now}");

                    return new Position()
                    {
                        Price = withOutLastKline.Last().Close,
                        Symbol = withOutLastKline.Last().Symbol,
                        TypePosition = withOutLastKline.Last().Close > withOutLastKline.Last().Open ? TypePosition.Long : TypePosition.Short,
                        CloseTime = withOutLastKline.Last().CloseTime
                    };
                }
            }

            return null;
        }
    }
}
