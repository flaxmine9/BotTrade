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
using System.Threading.Tasks.Dataflow;
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
        private List<string> _symbols { get; set; }
        private IEnumerable<Pump> _pumps { get; set; }

        private User _user { get; set; }
        private ApplicationContext _dataBase { get; set; }

        private string _nameUser { get; set; }

        private BufferBlock<IEnumerable<IEnumerable<Kline>>> _bufferKlines { get; set; }

        public Scalping(TradeSetting tradeSetting)
        {
            _bufferKlines = new BufferBlock<IEnumerable<IEnumerable<Kline>>>();
            _tradeSetting = tradeSetting;


            _symbols = new List<string>()
            {
                "BCHUSDT", "XMRUSDT", "COMPUSDT"
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
            #region new logic

            PipeLine pipeLine = new PipeLine(_trade, _user, _dataBase, _nameStrategy, waitAfterExitPosition: true);

            pipeLine.Create();

            for (uint i = 0; i < uint.MaxValue; i++)
            {
                if (pipeLine.CheckFreePositions())
                {
                    var klines = await _trade.GetLstKlinesAsync(_symbols, (KlineInterval)_tradeSetting.TimeFrame, 5);
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
                await Task.Delay(50);
            }

            #endregion
        }


        public async Task Start(string nameUser, string key, string secretKey, ApplicationContext dataBase, string typeNetBinance)
        {
            _trade = new Trade(key, secretKey, _tradeSetting, typeNetBinance);
            _dataBase = dataBase;
            _nameUser = nameUser;

            await _trade.SetExchangeInformationAsync();
            await _trade.SetTradeSettings(_symbols);

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
                decimal avrVolumeNineKlines = lstKlines.SkipLast(1).Average(x => x.QuoteVolume);
                if (lstKlines.Last().QuoteVolume >= avrVolumeNineKlines
                    //&& (lstKlines.Last().Close / lstKlines.Last().Open >= 1.0035m || lstKlines.Last().Open / lstKlines.Last().Close >= 1.0035m)
                    )
                {
                    if(lstKlines.Last().Close > lstKlines.Last().Open)
                    {
                        // long

                        signals.Add(new TradeSignal()
                        {
                            Price = lstKlines.Last().Close,
                            Symbol = lstKlines.Last().Symbol,
                            TypePosition = TypePosition.Long
                        });
                    }
                    else if(lstKlines.Last().Close < lstKlines.Last().Open)
                    {
                        // short

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

        public Task Start(string nameUser, string key, string secretKey, ApplicationContext dataBase)
        {
            throw new NotImplementedException();
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
