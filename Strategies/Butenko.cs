using Binance.Net.Enums;
using DataBase;
using DataBase.Models;
using Microsoft.EntityFrameworkCore;
using Skender.Stock.Indicators;
using Strategies.Data;
using Strategy.Interfaces;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using TechnicalIndicator.Models;
using TechnicalIndicator.Oscillator;
using TechnicalIndicator.Trend;
using TradeBinance;
using TradeBinance.Models;
using TradePipeLine;

namespace Strategies
{
    public class Butenko : IStrategy
    {
        private string _nameStrategy { get; set; } = "Butenko";

        private TradeSetting _tradeSetting { get; set; }
        private Trade _trade { get; set; }

        private User _user { get; set; }
        private ApplicationContext _dataBase { get; set; }


        #region Indicators

        private MACD _macd { get; set; }
        private EMA _ema { get; set; }
        private SuperTrend _superTrend { get; set; }

        #endregion

        private List<ButenkoData> _data { get; set; }

        public Butenko(TradeSetting tradeSetting)
        {
            _tradeSetting = tradeSetting;

            _macd = new MACD();
            _ema = new EMA();
            _superTrend = new SuperTrend();

            _data = new List<ButenkoData>()
            {
                new ButenkoData() { Symbol = "ETHUSDT", EmaFast = 30, EmaSLow = 50, AtrMult = 4.5m, AtrPeriod = 14, MacdFastPeriod = 12, MacdSlowPeriod = 26 },
                new ButenkoData() { Symbol = "BNBUSDT", EmaFast = 40, EmaSLow = 70, AtrMult = 3.1m, AtrPeriod = 18, MacdFastPeriod = 12, MacdSlowPeriod = 26 },
                new ButenkoData() { Symbol = "DOTUSDT", EmaFast = 35, EmaSLow = 110, AtrMult = 3.7m, AtrPeriod = 26, MacdFastPeriod = 12, MacdSlowPeriod = 26 },
                new ButenkoData() { Symbol = "XRPUSDT", EmaFast = 30, EmaSLow = 120, AtrMult = 4.1m, AtrPeriod = 28, MacdFastPeriod = 12, MacdSlowPeriod = 26 },
                new ButenkoData() { Symbol = "LINKUSDT", EmaFast = 15, EmaSLow = 90, AtrMult = 5.7m, AtrPeriod = 14, MacdFastPeriod = 12, MacdSlowPeriod = 26 },
                new ButenkoData() { Symbol = "LTCUSDT", EmaFast = 25, EmaSLow = 90, AtrMult = 6.9m, AtrPeriod = 16, MacdFastPeriod = 12, MacdSlowPeriod = 26 },
                new ButenkoData() { Symbol = "FLMUSDT", EmaFast = 30, EmaSLow = 60, AtrMult = 6.4m, AtrPeriod = 26, MacdFastPeriod = 12, MacdSlowPeriod = 26 }
            };
        }

        public async Task Logic()
        {
            PipeLine pipeLine = new PipeLine(_trade, _user, _dataBase, _nameStrategy, waitAfterExitPosition: true, 1000);

            pipeLine.Create();

            for (uint i = 0; i < uint.MaxValue; i++)
            {
                try
                {
                    if (pipeLine.CheckFreePositions())
                    {
                        var symbolsWithOutRunning = _data.Select(x => x.Symbol).Except(pipeLine.GetRunningPositions());

                        var klines = await _trade.GetLstKlinesAsync(symbolsWithOutRunning, (KlineInterval)_tradeSetting.TimeFrame, limit: 250);

                        if (klines.Any())
                        {
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
                    }
                    await WaitTime(_data.First().Symbol);
                }
                catch (Exception ex)
                {
                    Console.WriteLine(ex.Message);
                }
            }
        }

        public async Task Start(string nameUser, string key, string secretKey, ApplicationContext dataBase, string typeNetBinance)
        {
            _trade = new Trade(key, secretKey, _tradeSetting, typeNetBinance);
            _dataBase = dataBase;

            await _trade.SetExchangeInformationAsync();
            await _trade.SetTradeSettings(_data.Select(x => x.Symbol));

            _user = await _dataBase.Users.FirstOrDefaultAsync(x => x.Name.Equals(nameUser));
            if (_user != null)
            {
                Console.WriteLine($"User: {_user.Name}. Запущена стратегия {_nameStrategy}");
                await Logic();
            }
            else { Console.WriteLine($"Пользователь {nameUser} не найден"); }
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

                IEnumerable<Kline> withOutLastKline = lstKlines.SkipLast(1);

                ButenkoData data = _data.Where(x => withOutLastKline.First().Symbol.Equals(x.Symbol)).First();

                EmaResult fastEma = _ema.GetEma(withOutLastKline, data.EmaFast).Last();
                EmaResult lowEma = _ema.GetEma(withOutLastKline, data.EmaSLow).Last();

                MacdResult macd = _macd.GetMACD(withOutLastKline, data.MacdFastPeriod, data.MacdSlowPeriod, 9).Last();
                
                List<SuperTrendResult> superTrend = _superTrend.GetSuperTrend(withOutLastKline, data.AtrPeriod, multiplier: data.AtrMult).TakeLast(2).ToList();

                if(fastEma.Ema > lowEma.Ema 
                    && withOutLastKline.Last().Close > superTrend.Last().SuperTrend && withOutLastKline.SkipLast(1).Last().Close < superTrend.First().SuperTrend
                    && macd.Histogram > 0)
                {
                    signals.Add(new TradeSignal()
                    {
                        Price = withOutLastKline.Last().Close,
                        Symbol = withOutLastKline.Last().Symbol,
                        TypePosition = TypePosition.Long
                    });
                }
                else if(fastEma.Ema < lowEma.Ema
                    && withOutLastKline.Last().Close < superTrend.Last().SuperTrend && withOutLastKline.SkipLast(1).Last().Close > superTrend.First().SuperTrend
                    && macd.Histogram < 0)
                {
                    signals.Add(new TradeSignal()
                    {
                        Price = withOutLastKline.Last().Close,
                        Symbol = withOutLastKline.Last().Symbol,
                        TypePosition = TypePosition.Short
                    });               
                }
            }

            return signals;
        }

        private async Task WaitTime(string symbol)
        {
            var klineForTime = await _trade.GetKlineAsync(symbol, (KlineInterval)_tradeSetting.TimeFrame, limit: 1);
            if (klineForTime != null)
            {
                DateTime timeNow = DateTime.Now.ToUniversalTime();
                TimeSpan waitTime = klineForTime.CloseTime.AddMilliseconds(1050) - timeNow;

                await Task.Delay(Math.Abs((int)waitTime.TotalMilliseconds));
            }
        }
    }
}
