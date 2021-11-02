using Binance.Net.Enums;
using Binance.Net.Objects.Futures.FuturesData;
using Skender.Stock.Indicators;
using Strategies.Models;
using Strategy.Interfaces;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using TechnicalIndicator.Models;
using TechnicalIndicator.Trend;
using TradeBinance;
using TradeBinance.Models;
using System.Threading.Tasks.Dataflow;
using DataBase;
using DataBase.Models;
using Microsoft.EntityFrameworkCore;
using TradePipeLine;

namespace Strategies
{
    public class StrategySuperTrendSSL : IStrategy
    {
        private string _nameStrategy = nameof(StrategySuperTrendSSL);

        private Trade _trade { get; set; }
        private TradeSetting _tradeSetting { get; set; }

        private SSL _ssl { get; set; }
        private SuperTrend _superTrend { get; set; }

        private User _user { get; set; }

        private List<SuperTrendSSLData> _superTrendSSLData { get; set; }

        private ApplicationContext _dataBase { get; set; }

        private string _nameUser { get; set; }

        public StrategySuperTrendSSL(TradeSetting tradeSetting)
        {
            _tradeSetting = tradeSetting;

            _ssl = new SSL();
            _superTrend = new SuperTrend();

            _superTrendSSLData = new List<SuperTrendSSLData>()
            {
                //new SuperTrendSSLData() { Symbol = "ETHUSDT", Period = 60, ATRMultiplier = 2.4m, ATRPeriod = 17 },
                //new SuperTrendSSLData() { Symbol = "BNBUSDT", Period = 75, ATRMultiplier = 4.2m, ATRPeriod = 10 },
                //new SuperTrendSSLData() { Symbol = "DOTUSDT", Period = 50, ATRMultiplier = 6.4m, ATRPeriod = 22 },
                //new SuperTrendSSLData() { Symbol = "XRPUSDT", Period = 70, ATRMultiplier = 5.0m, ATRPeriod = 10 },
                //new SuperTrendSSLData() { Symbol = "LTCUSDT", Period = 60, ATRMultiplier = 4.2m, ATRPeriod = 13 },
                //new SuperTrendSSLData() { Symbol = "LINKUSDT", Period = 85, ATRMultiplier = 4.0m, ATRPeriod = 27 },
                //new SuperTrendSSLData() { Symbol = "FLMUSDT", Period = 90, ATRMultiplier = 6.4m, ATRPeriod = 10 }


                #region Test data

                new SuperTrendSSLData() { Symbol = "BTCUSDT", Period = 2, ATRMultiplier = 2, ATRPeriod = 2 },
                new SuperTrendSSLData() { Symbol = "ETHUSDT", Period = 2, ATRMultiplier = 2, ATRPeriod = 2 },
                //new SuperTrendSSLData() { Symbol = "BCHUSDT", Period = 2, ATRMultiplier = 2, ATRPeriod = 2 },
                //new SuperTrendSSLData() { Symbol = "XMRUSDT", Period = 2, ATRMultiplier = 2, ATRPeriod = 2 },
                //new SuperTrendSSLData() { Symbol = "COMPUSDT", Period = 2, ATRMultiplier = 2, ATRPeriod = 2 },
                new SuperTrendSSLData() { Symbol = "LTCUSDT", Period = 2, ATRMultiplier = 2, ATRPeriod = 2 }

                #endregion
            };
        }

        public async Task Logic()
        {
            PipeLine pipeLine = new PipeLine(_trade, _user, _dataBase, _nameStrategy, waitAfterExitPosition: false);

            pipeLine.Create();

            for (uint i = 0; i < uint.MaxValue; i++)
            {
                if (pipeLine.CheckFreePositions())
                {
                    var klines = await _trade.GetLstKlinesAsync(_superTrendSSLData.Select(x => x.Symbol), (KlineInterval)_tradeSetting.TimeFrame, 200);
                    IEnumerable<TradeSignal> signals = GetSignals(klines);

                    if (signals.Any())
                    {
                        var balanceUSDT = await _trade.GetBalanceAsync();
                        if (balanceUSDT != -1)
                        {
                            foreach (TradeSignal signal in signals)
                            {
                                if (balanceUSDT >= _tradeSetting.BalanceUSDT)
                                {
                                    if (pipeLine.CheckFreePositions())
                                    {
                                        balanceUSDT -= _tradeSetting.BalanceUSDT;

                                        pipeLine.AddSignal(signal);
                                    }
                                }
                                else { Console.WriteLine($"User: {_user.Name}. Баланс меньше {_tradeSetting.BalanceUSDT}"); break; }
                            }
                        }
                        else { continue; }
                    }
                }

                await WaitTime();
            }

            Console.WriteLine("Стратегия завершилась!");
        }

        private async Task WaitTime()
        {
            var klineForTime = await _trade.GetKlineAsync(_superTrendSSLData.First().Symbol, (KlineInterval)_tradeSetting.TimeFrame, limit: 1);
            if (klineForTime != null)
            {
                DateTime timeNow = DateTime.Now.ToUniversalTime();
                TimeSpan waitTime = klineForTime.CloseTime.AddMilliseconds(1050) - timeNow;

                await Task.Delay(Math.Abs((int)waitTime.TotalMilliseconds));
            }
        }

        public async Task Start(string nameUser, string key, string secretKey, ApplicationContext dataBase, string typeNetBinance)
        {
            _trade = new Trade(key, secretKey, _tradeSetting, typeNetBinance);
            _dataBase = dataBase;
            _nameUser = nameUser;

            await _trade.SetExchangeInformationAsync();
            await _trade.SetTradeSettings(_superTrendSSLData.Select(x => x.Symbol));

            _user = await _dataBase.Users.FirstOrDefaultAsync(x => x.Name.Equals(_nameUser));
            if (_user != null)
            {
                Console.WriteLine($"User: {_user.Name}. Запущена стратегия {_nameStrategy}");
                await Logic();
            }
            else { Console.WriteLine($"Пользователь {_nameUser} не найден"); }
        }

        private IEnumerable<TradeSignal> GetSignals(IEnumerable<IEnumerable<Kline>> klines)
        {
            List<TradeSignal> signals = new();

            foreach (IEnumerable<Kline> lstKlines in klines)
            {
                if (lstKlines.First().Symbol.Equals("RAYUSDT"))
                {
                    continue;
                }

                List<Kline> withOutLastKline = lstKlines.SkipLast(1).ToList();

                SuperTrendSSLData data = _superTrendSSLData.Where(x => withOutLastKline.First().Symbol.Equals(x.Symbol)).First();

                SSlValues ssl = _ssl.GetSSL(withOutLastKline, data.Period);
                SuperTrendResult superTrend = _superTrend.GetSuperTrend(withOutLastKline, data.ATRPeriod, data.ATRMultiplier).Last();

                if (superTrend.LowerBand != null && withOutLastKline.Last().Close > superTrend.LowerBand && _ssl.CrossOverLong(ssl))
                {
                    signals.Add(new TradeSignal()
                    {
                        Price = withOutLastKline.Last().Close,
                        Symbol = withOutLastKline.Last().Symbol,
                        TypePosition = withOutLastKline.Last().Close > withOutLastKline.Last().Open ? TypePosition.Long : TypePosition.Short
                    });
                }
                else if (superTrend.UpperBand != null && withOutLastKline.Last().Close < superTrend.UpperBand && _ssl.CrossOverShort(ssl))
                {
                    signals.Add(new TradeSignal()
                    {
                        Price = withOutLastKline.Last().Close,
                        Symbol = withOutLastKline.Last().Symbol,
                        TypePosition = withOutLastKline.Last().Close > withOutLastKline.Last().Open ? TypePosition.Long : TypePosition.Short,
                        CloseTime = withOutLastKline.Last().CloseTime
                    });
                }
            }

            return signals;
        }
    }
}
