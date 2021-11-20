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
using TechnicalIndicator.Trend;
using TradeBinance;
using TradeBinance.Models;
using TradePipeLine;

namespace Strategies
{
    public class ScalpingByTrend : IStrategy
    {
        private string _nameStrategy { get; set; } = "ScalpingByTrend";

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

        private List<ScalpingByTrendData> _data { get; set; }

        private int quantityKlines { get; set; }

        public ScalpingByTrend(TradeSetting tradeSetting)
        {
            _tradeSetting = tradeSetting;

            _sma = new SMA();
            _superTrend = new SuperTrend();
            _linearRegression = new LinearRegression();
            _roc = new ROC();

            _data = new List<ScalpingByTrendData>()
            {
                new ScalpingByTrendData() { Symbol = "ETHUSDT", SMAFastPeriod = 20, SMASlowPeriod = 60, LinearRegressionPeriod = 10, LinearRegressionSlopeValue = 7, RocPeriod = 30, RocSmoothPeriod = 2, RocValue = 0.7m, SuperTrendPeriod = 50, SuperTrendMultiplier = 4.5m },
                //new ScalpingByTrendData() { Symbol = "BNBUSDT", SMAFastPeriod = 25, SMASlowPeriod = 55, LinearRegressionPeriod = 20, LinearRegressionSlopeValue = 4, RocPeriod = 10, RocSmoothPeriod = 5, RocValue = 0.6m, SuperTrendPeriod = 50, SuperTrendMultiplier = 5 },
                //new ScalpingByTrendData() { Symbol = "LINKUSDT", SMAFastPeriod = 20, SMASlowPeriod = 50, LinearRegressionPeriod = 15, LinearRegressionSlopeValue = 1, RocPeriod = 30, RocSmoothPeriod = 2, RocValue = 2.5m, SuperTrendPeriod = 40, SuperTrendMultiplier = 6 },
                //new ScalpingByTrendData() { Symbol = "LTCUSDT", SMAFastPeriod = 30, SMASlowPeriod = 60, LinearRegressionPeriod = 10, LinearRegressionSlopeValue = 14, RocPeriod = 10, RocSmoothPeriod = 3, RocValue = 1.3m, SuperTrendPeriod = 30, SuperTrendMultiplier = 4.5m },
                //new ScalpingByTrendData() { Symbol = "XRPUSDT", SMAFastPeriod = 25, SMASlowPeriod = 55, LinearRegressionPeriod = 20, LinearRegressionSlopeValue = 6, RocPeriod = 40, RocSmoothPeriod = 5, RocValue = 0.7m, SuperTrendPeriod = 10, SuperTrendMultiplier = 6 }
            };
        }


        public async Task Logic()
        {
            PipeLine pipeLine = new PipeLine(_trade, _user, _dataBase, _nameStrategy, waitAfterExitPosition: false, 1000);

            pipeLine.Create();

            quantityKlines = _data.Max(x => x.RocPeriod) + 250;

            for (uint i = 0; i < uint.MaxValue; i++)
            {
                try
                {
                    if (pipeLine.CheckFreePositions())
                    {
                        var symbolsWithOutRunning = _data.Select(x => x.Symbol).Except(pipeLine.GetRunningPositions());

                        var klines = await _trade.GetLstKlinesAsync(symbolsWithOutRunning, (KlineInterval)_tradeSetting.TimeFrame, limit: quantityKlines);

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

                ScalpingByTrendData data = _data.Where(x => withOutLastKline.First().Symbol.Equals(x.Symbol)).First();

                SmaResult fastSma = _sma.GetEma(withOutLastKline, data.SMAFastPeriod).Last();
                SmaResult lowSma = _sma.GetEma(withOutLastKline, data.SMASlowPeriod).Last();

                IEnumerable<RocResult> roc = _roc.GetRoc(withOutLastKline, data.RocPeriod, data.RocSmoothPeriod).Where(x => x.RocSma != null);
                SlopeResult lr = _linearRegression.GetLinearRegression(roc.Select(x => new Kline()
                {
                    Close = x.RocSma.Value
                }), data.LinearRegressionPeriod).Last();

                SuperTrendResult superTrend = _superTrend.GetSuperTrend(withOutLastKline, data.SuperTrendPeriod, multiplier: data.SuperTrendMultiplier).Last();

                if(fastSma.Sma > lowSma.Sma && withOutLastKline.Last().Close > superTrend.SuperTrend
                    && roc.Last().RocSma > data.RocValue && lr.Slope * 100.0m > data.LinearRegressionSlopeValue
                    && withOutLastKline.Last().Close > withOutLastKline.Last().Open)
                {
                    signals.Add(new TradeSignal()
                    {
                        Price = withOutLastKline.Last().Close,
                        Symbol = withOutLastKline.Last().Symbol,
                        TypePosition = TypePosition.Long
                    });
                }
                else if(fastSma.Sma < lowSma.Sma && withOutLastKline.Last().Close < superTrend.SuperTrend
                    && roc.Last().RocSma < -data.RocValue && lr.Slope * 100.0m < -data.LinearRegressionSlopeValue
                    && withOutLastKline.Last().Close < withOutLastKline.Last().Open)
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
