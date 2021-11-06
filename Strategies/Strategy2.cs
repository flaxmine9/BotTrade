using Binance.Net.Enums;
using DataBase;
using DataBase.Models;
using Microsoft.EntityFrameworkCore;
using Skender.Stock.Indicators;
using Strategy.Interfaces;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TechnicalIndicator.Models;
using TechnicalIndicator.Oscillator;
using TechnicalIndicator.Trend;
using TradeBinance;
using TradeBinance.Models;
using TradePipeLine;

namespace Strategies
{
    public class Strategy2 : IStrategy
    {
        private string _nameStrategy { get; set; } = "Scalping";

        private TradeSetting _tradeSetting { get; set; }
        private Trade _trade { get; set; }

        private User _user { get; set; }
        private ApplicationContext _dataBase { get; set; }


        #region Indicators

        private MACD _macd { get; set; }
        private EMA _ema { get; set; }
        private SuperTrend _superTrend { get; set; }

        public Strategy2(TradeSetting tradeSetting)
        {
            _tradeSetting = tradeSetting;

            _macd = new MACD();
            _ema = new EMA();
            _superTrend = new SuperTrend();
        }

        #endregion

        public async Task Logic()
        {
            PipeLine pipeLine = new PipeLine(_trade, _user, _dataBase, _nameStrategy, waitAfterExitPosition: true, 1000);

            pipeLine.Create();

            for (uint i = 0; i < uint.MaxValue; i++)
            {
                if (pipeLine.CheckFreePositions())
                {
                    var klines = await _trade.GetLstKlinesAsync(new List<string>() { "LTCUSDT" }, (KlineInterval)_tradeSetting.TimeFrame, limit: 150);

                    //var klines = await _trade.GetLstKlinesAsync(new List<string>() { "LTCUSDT" }, (KlineInterval)_tradeSetting.TimeFrame,
                    //    startTime: new DateTime(2021, 11, 1, 10, 55, 0), new DateTime(2021, 11, 2, 7, 0, 0), limit: 250);

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
        }

        public async Task Start(string nameUser, string key, string secretKey, ApplicationContext dataBase, string typeNetBinance)
        {
            _trade = new Trade(key, secretKey, _tradeSetting, typeNetBinance);
            _dataBase = dataBase;

            await _trade.SetExchangeInformationAsync();
            //await _trade.SetTradeSettings(null);

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

                EmaResult fastEma = _ema.GetEma(withOutLastKline, 10).Last();
                EmaResult lowEma = _ema.GetEma(withOutLastKline, 20).Last();

                MacdResult macd = _macd.GetMACD(withOutLastKline, 12, 26, 9).Last();
                
                List<SuperTrendResult> superTrend = _superTrend.GetSuperTrend(withOutLastKline, 10, multiplier: 3).TakeLast(2).ToList();

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
                    });                }
            }

            return signals;
        }

        private async Task WaitTime()
        {
            var klineForTime = await _trade.GetKlineAsync(null, (KlineInterval)_tradeSetting.TimeFrame, limit: 1);
            if (klineForTime != null)
            {
                DateTime timeNow = DateTime.Now.ToUniversalTime();
                TimeSpan waitTime = klineForTime.CloseTime.AddMilliseconds(1050) - timeNow;

                await Task.Delay(Math.Abs((int)waitTime.TotalMilliseconds));
            }
        }
    }
}
