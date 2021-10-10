using Strategies.Models;
using Strategy.Interfaces;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Threading.Tasks.Dataflow;
using TechnicalIndicator.Models;
using TradeBinance;
using TradeBinance.Models;

namespace Strategies
{
    public class Scalping : IStrategy
    {
        private TradeSetting _tradeSetting { get; set; }
        private Trade _trade { get; set; }

        private bool flag { get; set; } = true;

        private List<string> _symbols { get; set; }
        private IEnumerable<Pump> _pumps { get; set; }

        private BufferBlock<IEnumerable<IEnumerable<Kline>>> _bufferKlines { get; set; }

        public Scalping(TradeSetting tradeSetting)
        {
            _bufferKlines = new BufferBlock<IEnumerable<IEnumerable<Kline>>>();
            _tradeSetting = tradeSetting;


            _symbols = new List<string>()
            {
                "ETHUSDT"
            };

            //_pumps = new List<Pump>()
            //{
            //    new Pump() { Symbol = "ATAUSDT", VolumeUSDT = 200000.0m },
            //    new Pump() { Symbol = "COTIUSDT", VolumeUSDT = 1000000.0m },
            //    new Pump() { Symbol = "FTMUSDT", VolumeUSDT = 9000000.0m },
            //    new Pump() { Symbol = "NKNUSDT", VolumeUSDT = 200000.0m },
            //    new Pump() { Symbol = "KEEPUSDT", VolumeUSDT = 90000.0m },
            //    new Pump() { Symbol = "ADAUSDT", VolumeUSDT = 2500000.0m },
            //};
        }

        public async Task Logic()
        {
            Random random = new Random();

            await _trade.SetTradeSettings(_symbols);

            for (uint i = 0; i < uint.MaxValue; i++)
            {
                IEnumerable<IEnumerable<Kline>> klines = await _trade.GetLstKlinesAsync(_symbols, limit: 3);

                List<Kline> klinesPumps = CheckPumpVolumesAsync(klines).ToList();
                if (klinesPumps.Any())
                {
                    Kline randomKlinePump = klinesPumps[random.Next(0, klinesPumps.Count)];

                    Console.WriteLine($"Symbol: {randomKlinePump.Symbol}\n" +
                        $"Time: {DateTime.Now.ToLocalTime()}");

                    TypePosition typePosition = randomKlinePump.Close > randomKlinePump.Open ? TypePosition.Long : TypePosition.Short;

                    bool entriedMarket = await _trade.EntryMarket(randomKlinePump.Symbol, price: randomKlinePump.Close, _tradeSetting.BalanceUSDT, typePosition);
                    if (entriedMarket)
                    {
                        Console.WriteLine("Зашли в позицию по валюте: {0}", randomKlinePump.Symbol);
                        var position = await _trade.GetCurrentOpenPositionAsync(randomKlinePump.Symbol);
                        if (position != null)
                        {
                            var gridOrders = _trade.GetGridOrders(position);
                            var placedOrders = await _trade.PlaceOrders(gridOrders);
                            if (placedOrders.Any())
                            {
                                Console.WriteLine("Поставили ордера по валюте {0}", randomKlinePump.Symbol);

                                await _trade.ControlOrders(placedOrders, randomKlinePump.Symbol, 100);

                                var klineForTime = await _trade.GetKlineAsync(randomKlinePump.Symbol, limit: 1);

                                if (klineForTime != null)
                                {
                                    var timeNow = DateTime.Now.ToUniversalTime();

                                    TimeSpan waitTime = klineForTime.CloseTime.AddMilliseconds(1100) - timeNow;

                                    Console.WriteLine($"Ждем завершения формирования свечи {klineForTime.Symbol}: {(int)waitTime.TotalSeconds} секунд");

                                    await Task.Delay((int)waitTime.TotalMilliseconds);

                                    Console.WriteLine("Свеча сформировалась! Ищем дальше сигналы");

                                }
                            }
                            else
                            {
                                Console.WriteLine($"Валюта: {randomKlinePump.Symbol} -- не удалось поставить ордера, пытаем поставить заново!");
                                placedOrders = await _trade.PlaceOrders(gridOrders);

                                Console.WriteLine("Поставили ордера по валюте {0}", randomKlinePump.Symbol);

                                await _trade.ControlOrders(placedOrders, randomKlinePump.Symbol, 100);
                            }
                        }
                        else { Console.WriteLine($"Валюта: {randomKlinePump.Symbol} -- Не удалось получить позицию"); }
                    }
                }
            }
        }


        public async Task Start(string key, string secretKey)
        {
            _trade = new Trade(key, secretKey, _tradeSetting);

            await _trade.SetExchangeInformationAsync();

            await Logic();
        }

        private IEnumerable<Kline> CheckPumpVolumesAsync(IEnumerable<IEnumerable<Kline>> klines)
        {
            List<Kline> list = new List<Kline>();

            foreach (IEnumerable<Kline> lstKlines in klines)
            {
                decimal avrVolumeNineKlines = lstKlines.SkipLast(1).Average(x => x.QuoteVolume);
                if (lstKlines.Last().QuoteVolume >= avrVolumeNineKlines
                    //&& (lstKlines.Last().Close / lstKlines.Last().Open >= 1.0035m || lstKlines.Last().Open / lstKlines.Last().Close >= 1.0035m)
                    )
                {
                    list.Add(lstKlines.Last());
                }
            }

            return list;
        }

        //private IEnumerable<Kline> CheckPumpVolumesAsync(IEnumerable<IEnumerable<Kline>> klines)
        //{
        //    List<Kline> list = new List<Kline>();

        //    foreach (IEnumerable<Kline> lstKlines in klines)
        //    {
        //        decimal volumeUSDT = _pumps.Where(x => x.Symbol.Equals(lstKlines.Last().Symbol)).First().VolumeUSDT;
        //        if (lstKlines.Last().QuoteVolume >= volumeUSDT
        //            && (lstKlines.Last().Close / lstKlines.Last().Open >= 1.005m || lstKlines.Last().Open / lstKlines.Last().Close >= 1.005m))
        //        {
        //            list.Add(lstKlines.Last());
        //        }
        //    }

        //    return list;
        //}
    }
}
