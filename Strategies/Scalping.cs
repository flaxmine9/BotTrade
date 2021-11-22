using Binance.Net.Enums;
using DataBase;
using DataBase.Models;
using Microsoft.EntityFrameworkCore;
using Skender.Stock.Indicators;
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
    public class Scalping : IStrategy
    {
        private string _nameStrategy { get; set; } = "Scalping";

        private TradeSetting _tradeSetting { get; set; }
        private Trade _trade { get; set; }
       
        private User _user { get; set; }
        private ApplicationContext _dataBase { get; set; }

        private ROC _roc { get; set; }
        private List<string> _symbols { get; set; }

        public Scalping(TradeSetting tradeSetting)
        {
            _tradeSetting = tradeSetting;
            _roc = new ROC();

            _symbols = new List<string>() 
            { 
                "IOTXUSDT"
            };
        }

        public async Task Logic()
        {
            #region new logic

            PipeLine pipeLine = new PipeLine(_trade, _user, _dataBase, _nameStrategy, waitAfterExitPosition: true, 100);

            pipeLine.Create();

            for (uint i = 0; i < uint.MaxValue; i++)
            {
                try
                {
                    if (pipeLine.CheckFreePositions())
                    {
                        var symbolsWithOutRunning = _symbols.Except(pipeLine.GetRunningPositions());

                        var klines = await _trade.GetLstKlinesAsync(symbolsWithOutRunning, (KlineInterval)_tradeSetting.TimeFrame, limit: 1);
                       
                        if (klines.Any())
                        {
                            List<TradeSignal> signals = GetSignals(klines).ToList();

                            if (signals.Any())
                            {
                                var balanceUSDT = await _trade.GetBalanceAsync();
                                if (balanceUSDT != -1)
                                {
                                    foreach (TradeSignal signal in signals)
                                    {
                                        Console.WriteLine($"Время закрытия свечи: {signal.CloseTime}");

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
                    await Task.Delay(200);
                }
                catch (Exception ex)
                {
                    Console.WriteLine(ex.Message);
                }
            }

            #endregion
        }


        public async Task Start(string nameUser, string key, string secretKey, ApplicationContext dataBase, string typeNetBinance)
        {
            _trade = new Trade(key, secretKey, _tradeSetting, typeNetBinance);
            _dataBase = dataBase;

            await _trade.SetExchangeInformationAsync();
            await _trade.SetTradeSettings(_symbols);

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
                if (lstKlines.Last().Close / lstKlines.Last().Open >= 1.025m)
                {
                    signals.Add(new TradeSignal()
                    {
                        Price = lstKlines.Last().Close,
                        Symbol = lstKlines.Last().Symbol,
                        CloseTime = lstKlines.Last().CloseTime,
                        TypePosition = TypePosition.Long
                    });
                }
                else if (lstKlines.Last().Open / lstKlines.Last().Close >= 1.025m)
                {
                    signals.Add(new TradeSignal()
                    {
                        Price = lstKlines.Last().Close,
                        Symbol = lstKlines.Last().Symbol,
                        CloseTime = lstKlines.Last().CloseTime,
                        TypePosition = TypePosition.Short
                    });
                }

            }

            return signals;
        }
    }
}
