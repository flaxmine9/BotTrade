using Binance.Net.Enums;
using DataBase;
using DataBase.Models;
using Microsoft.EntityFrameworkCore;
using Strategies.Models;
using Strategy.Interfaces;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using TechnicalIndicator.Models;
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

        private List<PumpData> _pumpData { get; set; }

        public Scalping(TradeSetting tradeSetting)
        {
            _tradeSetting = tradeSetting;

            _pumpData = new List<PumpData> 
            { 
                new PumpData() { Symbol = "KEEPUSDT", LimitVolume = 2000000.0m, TakeProfit = 1.025m, StopLoss = 1.015m },
                new PumpData() { Symbol = "IOTXUSDT", LimitVolume = 20000000.0m, TakeProfit = 1.0125m, StopLoss = 1.0125m },
                new PumpData() { Symbol = "COTIUSDT", LimitVolume = 10000000.0m, TakeProfit = 1.035m, StopLoss = 1.0175m },
                new PumpData() { Symbol = "FTMUSDT", LimitVolume = 9000000.0m, TakeProfit = 1.02m, StopLoss = 1.0125m },
                new PumpData() { Symbol = "DODOUSDT", LimitVolume = 1500000.0m, TakeProfit = 1.025m, StopLoss = 1.015m }
            };
        }

        public async Task Logic()
        {
            #region new logic

            PipeLine pipeLine = new PipeLine(_trade, _user, _dataBase, _nameStrategy, waitAfterExitPosition: true);

            pipeLine.Create();

            for (uint i = 0; i < uint.MaxValue; i++)
            {
                if (pipeLine.CheckFreePositions())
                {
                    var klines = await _trade.GetLstKlinesAsync(_pumpData.Select(x => x.Symbol), (KlineInterval)_tradeSetting.TimeFrame, limit: 1);
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
                await Task.Delay(50);
            }

            #endregion
        }


        public async Task Start(string nameUser, string key, string secretKey, ApplicationContext dataBase, string typeNetBinance)
        {
            _trade = new Trade(key, secretKey, _tradeSetting, typeNetBinance);
            _dataBase = dataBase;

            await _trade.SetExchangeInformationAsync();
            await _trade.SetTradeSettings(_pumpData.Select(x => x.Symbol));

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
                decimal limitVolume = _pumpData.Where(x => x.Symbol.Equals(lstKlines.Last().Symbol)).First().LimitVolume;

                if (lstKlines.Last().BaseVolume >= limitVolume)
                {
                    if (lstKlines.Last().Close > lstKlines.Last().Open)
                    {
                        signals.Add(new TradeSignal()
                        {
                            Price = lstKlines.Last().Close,
                            Symbol = lstKlines.Last().Symbol,
                            TypePosition = TypePosition.Long
                        });
                    }
                    else if (lstKlines.Last().Close < lstKlines.Last().Open)
                    {
                        signals.Add(new TradeSignal()
                        {
                            Price = lstKlines.Last().Close,
                            Symbol = lstKlines.Last().Symbol,
                            TypePosition = TypePosition.Short
                        });
                    }
                }
            }

            return signals;
        }
    }
}
