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
using System.Text;
using System.Threading.Tasks;
using TechnicalIndicator.Models;
using TechnicalIndicator.Trend;
using TradeBinance;
using TradeBinance.Models;
using TradePipeLine;

namespace Strategies
{
    public class Butenko2 : IStrategy
    {
        private string _nameStrategy { get; set; } = "Butenko2";

        private TradeSetting _tradeSetting { get; set; }
        private Trade _trade { get; set; }

        private User _user { get; set; }
        private ApplicationContext _dataBase { get; set; }


        #region Indicators

        private LinearRegression _linearRegression { get; set; }
        private SMA _sma { get; set; }
        private SuperTrend _superTrend { get; set; }
        public ROC _roc { get; set; }

        #endregion

        private List<Butenko2Data> _data { get; set; }

        public Butenko2(TradeSetting tradeSetting)
        {
            _tradeSetting = tradeSetting;

            _sma = new SMA();
            _superTrend = new SuperTrend();
            _linearRegression = new LinearRegression();
            _roc = new ROC();

            _data = new List<Butenko2Data>()
            {
                new Butenko2Data() { Symbol = "ETHUSDT", SMAFastPeriod = 25, SMASlowPeriod = 70, LinearRegressionPeriod = 4, LinearRegressionSlopeValue = 10, RocPeriod = 20, RocSmoothPeriod = 2, RocValue = 1, SuperTrendPeriod = 20, SuperTrendMultiplier = 5}
            };
        }


        public async Task Logic()
        {
            PipeLine pipeLine = new PipeLine(_trade, _user, _dataBase, _nameStrategy, waitAfterExitPosition: false, 1000);

            pipeLine.Create();

            for (uint i = 0; i < uint.MaxValue; i++)
            {
                try
                {
                    if (pipeLine.CheckFreePositions())
                    {
                        var symbolsWithOutRunning = _data.Select(x => x.Symbol).Except(pipeLine.GetRunningPositions());

                        var klines = await _trade.GetLstKlinesAsync(symbolsWithOutRunning, (KlineInterval)_tradeSetting.TimeFrame, limit: 350);

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
                IEnumerable<Kline> withOutLastKline = lstKlines.SkipLast(1);

                Butenko2Data data = _data.Where(x => withOutLastKline.First().Symbol.Equals(x.Symbol)).First();

                SmaResult fastSma = _sma.GetEma(withOutLastKline, data.SMAFastPeriod).Last();
                SmaResult lowSma = _sma.GetEma(withOutLastKline, data.SMASlowPeriod).Last();

                IEnumerable<RocResult> roc = _roc.GetRoc(withOutLastKline, data.RocPeriod, data.RocSmoothPeriod).Where(x => x.RocSma != null);
                SlopeResult lr = _linearRegression.GetLinearRegression(roc.Select(x => new Kline()
                {
                    Close = x.RocSma.Value
                }), data.LinearRegressionPeriod).Last();

                SuperTrendResult superTrend = _superTrend.GetSuperTrend(withOutLastKline, data.SuperTrendPeriod, multiplier: data.SuperTrendMultiplier).Last();

                if(fastSma.Sma > lowSma.Sma && withOutLastKline.Last().Close > superTrend.SuperTrend
                    && roc.Last().RocSma > data.RocValue && lr.Slope > data.LinearRegressionSlopeValue)
                {
                    signals.Add(new TradeSignal()
                    {
                        Price = withOutLastKline.Last().Close,
                        Symbol = withOutLastKline.Last().Symbol,
                        TypePosition = TypePosition.Long
                    });
                }
                else if(fastSma.Sma < lowSma.Sma && withOutLastKline.Last().Close < superTrend.SuperTrend
                    && roc.Last().RocSma < -data.RocValue && lr.Slope < -data.LinearRegressionSlopeValue)
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
