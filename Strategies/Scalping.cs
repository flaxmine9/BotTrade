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

        private BufferBlock<IEnumerable<IEnumerable<Kline>>> _bufferKlines { get; set; }

        public Scalping(TradeSetting tradeSetting)
        {
            _bufferKlines = new BufferBlock<IEnumerable<IEnumerable<Kline>>>();
            _tradeSetting = tradeSetting;


            _symbols = new List<string>()
            {
                "FTMUSDT", "LRCUSDT", "KEEPUSDT",
                "BALUSDT", "DODOUSDT", "SCUSDT", "AKROUSDT",
                "DGBUSDT", "SFPUSDT", "STMXUSDT", "ALPHAUSDT",
                "NKNUSDT", "OCEANUSDT", "ATAUSDT", "BELUSDT",
                "GRTUSDT", "FLMUSDT", "SFPUSDT", "ETHUSDT",
                "COTIUSDT", "ADAUSDT"
            };
        }

        public async Task Logic()
        {
            Random random = new Random();

            await _trade.SetTradeSettings(_symbols);

            for (uint i = 0; i < uint.MaxValue; i++)
            {
                IEnumerable<IEnumerable<Kline>> klines = await _trade.GetLstKlinesAsync(_symbols, limit: 5);

                List<Kline> klinesPumps = CheckPumpVolumesAsync(klines).ToList();
                if (klinesPumps.Any())
                {
                    Kline randomKlinePump = klinesPumps[random.Next(0, klinesPumps.Count)];

                    Console.WriteLine($"Symbol: {randomKlinePump.Symbol}\n " +
                        $"Time: {DateTime.Now.ToLocalTime()}");

                    TypePosition typePosition = randomKlinePump.Close > randomKlinePump.Open ? TypePosition.Long : TypePosition.Short;

                    bool entriedMarket = await _trade.EntryMarket(randomKlinePump.Symbol, _tradeSetting.PartOfBalance, typePosition);
                    if (entriedMarket)
                    {
                        Console.WriteLine("Зашли в позицию по валюте: {0}", randomKlinePump.Symbol);
                        var position = await _trade.GetCurrentOpenPositionAsync(randomKlinePump.Symbol);
                        if (position != null)
                        {
                            var gridOrders = _trade.GetGridOrders(position);
                            await _trade.PlaceOrders(gridOrders);

                            Console.WriteLine("Поставили ордера по валюте {0}", randomKlinePump.Symbol);

                            await _trade.ControlOrders(randomKlinePump.Symbol);

                            var klineForTime = await _trade.GetKlineAsync(randomKlinePump.Symbol, limit: 1);

                            if (klineForTime != null)
                            {
                                var timeNow = DateTime.Now.ToUniversalTime();

                                TimeSpan waitTime = klineForTime.CloseTime.AddMilliseconds(200) - timeNow;

                                Console.WriteLine($"Ждем завершения формирования свечи {klineForTime.Symbol}: {(int)waitTime.TotalSeconds} секунд");

                                await Task.Delay((int)waitTime.TotalMilliseconds);

                                Console.WriteLine("Свеча сформировалась! Ищем дальше сигналы");

                            }
                        }
                    }
                }
                //await Task.Delay(200);
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
                decimal avrVolumeNineKlines = lstKlines.SkipLast(1).Average(x => x.Volume);
                if (lstKlines.Last().Volume >= avrVolumeNineKlines * 3.0m
                    && (lstKlines.Last().Close / lstKlines.Last().Open >= 1.003m || lstKlines.Last().Open / lstKlines.Last().Close >= 1.003m))
                {
                    list.Add(lstKlines.Last());
                }
            }

            return list;
        }
    }
}
