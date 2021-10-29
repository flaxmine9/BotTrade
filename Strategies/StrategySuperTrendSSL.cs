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

        private BufferBlock<Signal> _bufferPositions { get; set; }
        private BufferBlock<string> _bufferFailedPosition { get; set; }
        private BufferBlock<Signal> _bufferFailedEntryMarket { get; set; }
        private BufferBlock<GridOrder> _bufferFailedPlaceOrders { get; set; }
        private BufferBlock<BinanceFuturesOrder> _bufferFirstFinishedOrder { get; set; }
        private BufferBlock<IEnumerable<BinanceFuturesUsdtTrade>> _bufferWriteTradeHistoryToDB { get; set; }

        private List<SuperTrendSSLData> _superTrendSSLData { get; set; }
        private List<Signal> _runningPositions { get; set; }

        private readonly object locker = new object();

        private ApplicationContext _dataBase { get; set; }

        private string _nameUser { get; set; }

        public StrategySuperTrendSSL(TradeSetting tradeSetting)
        {
            _tradeSetting = tradeSetting;

            _ssl = new SSL();
            _superTrend = new SuperTrend();

            _superTrendSSLData = new List<SuperTrendSSLData>()
            {
                new SuperTrendSSLData() { Symbol = "ETHUSDT", Period = 60, ATRMultiplier = 2.4m, ATRPeriod = 17 },
                new SuperTrendSSLData() { Symbol = "BNBUSDT", Period = 75, ATRMultiplier = 4.2m, ATRPeriod = 10 },
                new SuperTrendSSLData() { Symbol = "DOTUSDT", Period = 50, ATRMultiplier = 6.4m, ATRPeriod = 22 },
                new SuperTrendSSLData() { Symbol = "XRPUSDT", Period = 70, ATRMultiplier = 5.0m, ATRPeriod = 10 },
                new SuperTrendSSLData() { Symbol = "LTCUSDT", Period = 60, ATRMultiplier = 4.2m, ATRPeriod = 13 },
                new SuperTrendSSLData() { Symbol = "LINKUSDT", Period = 85, ATRMultiplier = 4.0m, ATRPeriod = 27 },
                new SuperTrendSSLData() { Symbol = "FLMUSDT", Period = 90, ATRMultiplier = 6.4m, ATRPeriod = 10 },
                new SuperTrendSSLData() { Symbol = "LUNAUSDT", Period = 75, ATRMultiplier = 5.6m, ATRPeriod = 29 }


                #region Test data

                //new SuperTrendSSLData() { Symbol = "BTCUSDT", Period = 2, ATRMultiplier = 2, ATRPeriod = 2 },
                //new SuperTrendSSLData() { Symbol = "ETHUSDT", Period = 2, ATRMultiplier = 2, ATRPeriod = 2 },
                //new SuperTrendSSLData() { Symbol = "BCHUSDT", Period = 2, ATRMultiplier = 2, ATRPeriod = 2 },
                //new SuperTrendSSLData() { Symbol = "XMRUSDT", Period = 2, ATRMultiplier = 2, ATRPeriod = 2 },
                //new SuperTrendSSLData() { Symbol = "COMPUSDT", Period = 2, ATRMultiplier = 2, ATRPeriod = 2 },
                //new SuperTrendSSLData() { Symbol = "LTCUSDT", Period = 2, ATRMultiplier = 2, ATRPeriod = 2 }

                #endregion
            };

            _bufferPositions = new();
            _runningPositions = new();

            _bufferFailedPosition = new();
            _bufferFailedEntryMarket = new();
            _bufferFailedPlaceOrders = new();

            _bufferFirstFinishedOrder = new();
            _bufferWriteTradeHistoryToDB = new BufferBlock<IEnumerable<BinanceFuturesUsdtTrade>>();
        }

        public async Task Logic()
        {
            #region pipeline

            var entryMarket = new TransformBlock<Signal, string>(async signal =>
            {
                string entriedMarket = await _trade.EntryMarket(signal.Symbol, price: signal.Price, _tradeSetting.BalanceUSDT, signal.TypePosition);
                if (entriedMarket != null)
                {
                    Console.WriteLine($"User: {_nameUser} -- Зашли по рынку по монете {signal.Symbol}");
                    return entriedMarket;
                }
                else
                {
                    await _bufferFailedEntryMarket.SendAsync(signal);
                    return "";
                }

            }, new ExecutionDataflowBlockOptions
            {
                MaxDegreeOfParallelism = DataflowBlockOptions.Unbounded,
                EnsureOrdered = false
            });

            var currentPosition = new TransformBlock<string, BinancePositionDetailsUsdt>(async symbol =>
            {
                var resultPosition = await _trade.GetCurrentOpenPositionAsync(symbol);
                if (resultPosition != null)
                {
                    Console.WriteLine($"User: {_nameUser} -- Получили позицию по монете {symbol}");
                    return resultPosition;
                }
                else
                {
                    await _bufferFailedPosition.SendAsync(symbol);
                    return null;
                }

            }, new ExecutionDataflowBlockOptions
            {
                MaxDegreeOfParallelism = DataflowBlockOptions.Unbounded,
                EnsureOrdered = false
            });

            var gridOrders = new TransformBlock<BinancePositionDetailsUsdt, GridOrder>(position =>
            {
                try
                {
                    var orders = _trade.GetGridOrders(position);
                    if (orders.ClosePositionOrders.Any() || orders.LimitOrders.Any())
                    {
                        return orders;
                    }
                    else
                    {
                        return new GridOrder()
                        {
                            ClosePositionOrders = new List<BinanceFuturesPlacedOrder>(),
                            LimitOrders = new List<BinanceFuturesBatchOrder>()
                        };
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Ошибка в блоке gridOrders\n" +
                        $"{ex.Message}");

                    return null;
                }
            }, new ExecutionDataflowBlockOptions() { MaxDegreeOfParallelism = 1, EnsureOrdered = false });

            var placeOrders = new TransformBlock<GridOrder, IEnumerable<BinanceFuturesPlacedOrder>>(async gridOrders =>
            {
                List<BinanceFuturesPlacedOrder> lst = new();

                try
                {
                    IEnumerable<BinanceFuturesPlacedOrder> placedOrders = (await _trade.PlaceOrders(gridOrders)).Where(x => x != null);
                    if (placedOrders.Any())
                    {
                        lst.AddRange(placedOrders);

                        Console.WriteLine($"User: {_nameUser} -- Поставили ордера по монете {lst.First().Symbol}");
                    }
                    else
                    {
                        Console.WriteLine($"Валюта: {gridOrders.ClosePositionOrders.First().Symbol} -- отправляем ордера в failed block");

                        if (await _bufferFailedPlaceOrders.SendAsync(gridOrders))
                        {
                            Console.WriteLine("Отправили ордера в failed block");
                        }
                    }

                    return lst;
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Ошибка в блоке placeOrders\n" +
                        $"{ex.Message}");

                    return lst;
                }

            }, new ExecutionDataflowBlockOptions
            {
                MaxDegreeOfParallelism = DataflowBlockOptions.Unbounded,
                EnsureOrdered = false
            });

            var contollOrders = new TransformBlock<IEnumerable<BinanceFuturesPlacedOrder>, string>(async placedOrders =>
            {
                try
                {
                    IEnumerable<BinanceFuturesOrder> finishedOrders = await _trade.ControlOrders(placedOrders, 1000);
                    if (finishedOrders.Any())
                    {
                        Console.WriteLine($"User: {_nameUser} -- выполнились ордера по валюте: {finishedOrders.First().Symbol}");
                        var minTimeOrder = finishedOrders.OrderBy(x => x.UpdateTime).First();

                        if (await _bufferFirstFinishedOrder.SendAsync(minTimeOrder))
                        {
                            Console.WriteLine($"User: {_nameUser} -- Передали ордера в буфер");
                        }

                        return finishedOrders.First().Symbol;
                    }

                    return "";
                }
                catch (Exception ex)
                {
                    Console.WriteLine("Ошибка в блоке controllOrders\n" +
                        ex.Message);

                    return "";
                }

            }, new ExecutionDataflowBlockOptions { MaxDegreeOfParallelism = DataflowBlockOptions.Unbounded, EnsureOrdered = false });


            var getTradeHistoryOrders = new ActionBlock<BinanceFuturesOrder>(async _ =>
            {
                IEnumerable<BinanceFuturesUsdtTrade> historyTrades = await _trade.GetTradeHistory(_.Symbol, _.UpdateTime, limit: _tradeSetting.MaxOrders * 5);
                if (!historyTrades.Any())
                {
                    Console.WriteLine($"{_nameUser} -- не удалось получить историю ордерова по валюте {_.Symbol}");
                }
                else
                {
                    if (await _bufferWriteTradeHistoryToDB.SendAsync(historyTrades))
                    {
                        Console.WriteLine($"User: {_nameUser} -- передали историю ордеров по валюте {historyTrades.First().Symbol}");
                    }
                }

            }, new ExecutionDataflowBlockOptions { MaxDegreeOfParallelism = DataflowBlockOptions.Unbounded, EnsureOrdered = false });


            var writeTradeHistoryToDB = new ActionBlock<IEnumerable<BinanceFuturesUsdtTrade>>(async _ =>
            {
                Console.WriteLine($"User: {_nameUser} -- получили историю ордеров по валюте {_.First().Symbol}");

                try
                {
                    var orders = _.Select(x => new Order()
                    {
                        NameStrategy = _nameStrategy,
                        UserId = _user.Id,
                        PositionSide = x.Side.Equals(OrderSide.Buy) ? PositionSide.Short.ToString() : PositionSide.Long.ToString(),
                        Commission = x.Commission,
                        Price = x.Price,
                        Quantity = x.Quantity,
                        RealizedPnl = x.RealizedPnl,
                        Symbol = x.Symbol,
                        Side = x.Side.ToString(),
                        TradeTime = x.TradeTime,
                        OrderId = x.OrderId
                    }).ToList();

                    await _dataBase.Orders.AddRangeAsync(orders);
                    await _dataBase.SaveChangesAsync();

                    Console.WriteLine($"User: {_nameUser} -- записали выполненные ордера в базу данных по валюте {_.First().Symbol}");

                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Ошибка в блоке writeTradeHistoryToDB\n" +
                        $"{ex.Message}");
                }

            }, new ExecutionDataflowBlockOptions { MaxDegreeOfParallelism = 1 });


            var finishedPosition = new ActionBlock<string>(symbol =>
            {
                DeletePosition(symbol);
            }, new ExecutionDataflowBlockOptions { MaxDegreeOfParallelism = DataflowBlockOptions.Unbounded, EnsureOrdered = false });

            #endregion

            #region failed

            var tryExecuteFailedPosition = new TransformBlock<string, BinancePositionDetailsUsdt>(async symbol =>
            {
                BinancePositionDetailsUsdt position = null;

                Console.WriteLine($"Валюта: {symbol} -- Пытаем получить 5 раз текущую позицию");

                for (int i = 0; i < 5; i++)
                {
                    var resultPosition = await _trade.GetCurrentOpenPositionAsync(symbol);
                    if (resultPosition != null)
                    {
                        Console.WriteLine($"Валюта: {symbol} -- Получили failed position");
                        position = resultPosition;

                        break;
                    }
                }

                Console.WriteLine($"Валюта: {symbol} -- Не удалось получить текущую позицию за 5 попыток");

                return position;

            }, new ExecutionDataflowBlockOptions
            {
                MaxDegreeOfParallelism = DataflowBlockOptions.Unbounded,
                EnsureOrdered = false
            });


            var tryExecuteFailedPlaceOrders = new TransformBlock<GridOrder, IEnumerable<BinanceFuturesPlacedOrder>>(async gridOrders =>
            {
                List<BinanceFuturesPlacedOrder> lst = new();

                Console.WriteLine($"Валюта: {gridOrders.ClosePositionOrders.First().Symbol} -- Пытаемся поставить ордера 5 раз");
                for (int i = 0; i < 5; i++)
                {
                    IEnumerable<BinanceFuturesPlacedOrder> placedOrders = await _trade.PlaceOrders(gridOrders);
                    if (placedOrders.Any())
                    {
                        Console.WriteLine($"Валюта: {placedOrders.First().Symbol} -- Поставили failed orders");

                        lst.AddRange(placedOrders);

                        break;
                    }
                }

                Console.WriteLine($"Валюта: {gridOrders.ClosePositionOrders.First().Symbol} -- Не удалось выставить ордера за 5 попыток");

                DeletePosition(gridOrders.ClosePositionOrders.First().Symbol);

                return lst;

            }, new ExecutionDataflowBlockOptions
            {
                MaxDegreeOfParallelism = DataflowBlockOptions.Unbounded,
                EnsureOrdered = false
            });

            var tryExecuteFailedEntryMarket = new TransformBlock<Signal, string>(async signal =>
            {
                Console.WriteLine($"Валюта: {signal.Symbol} -- Пытаемся зайти в рынок 5 раз");
                for (int i = 0; i < 5; i++)
                {
                    string entriedMarket = await _trade.EntryMarket(signal.Symbol, price: signal.Price, _tradeSetting.BalanceUSDT, signal.TypePosition);
                    if (entriedMarket != null)
                    {
                        Console.WriteLine($"Валюта: {signal.Symbol} -- Зашли в failed entry market");
                        return entriedMarket;
                    }
                }

                Console.WriteLine($"Валюта: {signal.Symbol} -- Не удалось зайти в рынок за 5 попыток");

                DeletePosition(signal.Symbol);

                return null;

            }, new ExecutionDataflowBlockOptions
            {
                MaxDegreeOfParallelism = DataflowBlockOptions.Unbounded,
                EnsureOrdered = false
            });

            #endregion

            var options = new DataflowLinkOptions() { PropagateCompletion = true };

            #region true

            _bufferPositions.LinkTo(entryMarket, options);
            entryMarket.LinkTo(currentPosition, options, x => x != "");
            entryMarket.LinkTo(DataflowBlock.NullTarget<string>());

            currentPosition.LinkTo(gridOrders, options, x => x != null);
            currentPosition.LinkTo(DataflowBlock.NullTarget<BinancePositionDetailsUsdt>());

            gridOrders.LinkTo(placeOrders, options, x => x != null);
            gridOrders.LinkTo(DataflowBlock.NullTarget<GridOrder>());

            placeOrders.LinkTo(contollOrders, options, x => x.Any());
            placeOrders.LinkTo(DataflowBlock.NullTarget<IEnumerable<BinanceFuturesPlacedOrder>>());

            contollOrders.LinkTo(finishedPosition, options, x => x != "");
            contollOrders.LinkTo(DataflowBlock.NullTarget<string>());

            _bufferFirstFinishedOrder.LinkTo(getTradeHistoryOrders, options);
            _bufferWriteTradeHistoryToDB.LinkTo(writeTradeHistoryToDB, options);

            #endregion

            #region failed

            _bufferFailedEntryMarket.LinkTo(tryExecuteFailedEntryMarket, options);
            tryExecuteFailedEntryMarket.LinkTo(currentPosition, options, x => x != "");
            tryExecuteFailedEntryMarket.LinkTo(DataflowBlock.NullTarget<string>());

            _bufferFailedPosition.LinkTo(tryExecuteFailedPosition, options);
            tryExecuteFailedPosition.LinkTo(gridOrders, options, x => x != null);
            tryExecuteFailedPosition.LinkTo(DataflowBlock.NullTarget<BinancePositionDetailsUsdt>());

            _bufferFailedPlaceOrders.LinkTo(tryExecuteFailedPlaceOrders, options, x => x != null);
            tryExecuteFailedPlaceOrders.LinkTo(contollOrders, options, x => x.Any());
            tryExecuteFailedPlaceOrders.LinkTo(DataflowBlock.NullTarget<IEnumerable<BinanceFuturesPlacedOrder>>());

            #endregion


            var produceSignals = ProduceSignals(_superTrendSSLData, maxPositions: _tradeSetting.MaxPositions);

            await produceSignals;

            Console.WriteLine("Стратегия завершилась!");
        }

        private void DeletePosition(string symbol)
        {
            lock (locker)
            {
                int index = _runningPositions.FindIndex(0, _runningPositions.Count, x => x.Symbol.Equals(symbol));
                if (index != -1)
                {
                    _runningPositions.RemoveAt(index);

                    Console.WriteLine($"Валюта: {symbol} -- Удалили выполненную позицию из спиcка\n" +
                        $"Осталось активных позииций {_nameUser}: {_runningPositions.Count}\n" +
                        $"Время: {DateTime.Now.ToUniversalTime()}");
                }
            }
        }

        private async Task WaitTime()
        {
            var klineForTime = await _trade.GetKlineAsync(_superTrendSSLData.First().Symbol, (KlineInterval)_tradeSetting.TimeFrame, limit: 1);
            if (klineForTime != null)
            {
                DateTime timeNow = DateTime.Now.ToUniversalTime();
                TimeSpan waitTime = klineForTime.CloseTime.AddMilliseconds(1000) - timeNow;

                await Task.Delay(Math.Abs((int)waitTime.TotalMilliseconds));
            }
        }

        public async Task Start(string nameUser, string key, string secretKey, ApplicationContext dataBase)
        {
            _trade = new Trade(key, secretKey, _tradeSetting);
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

        private IEnumerable<Signal> GetSignals(IEnumerable<IEnumerable<Kline>> klines)
        {
            List<Signal> signals = new();

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
                    signals.Add(new Signal()
                    {
                        Price = withOutLastKline.Last().Close,
                        Symbol = withOutLastKline.Last().Symbol,
                        TypePosition = withOutLastKline.Last().Close > withOutLastKline.Last().Open ? TypePosition.Long : TypePosition.Short
                    });
                }
                else if (superTrend.UpperBand != null && withOutLastKline.Last().Close < superTrend.UpperBand && _ssl.CrossOverShort(ssl))
                {
                    signals.Add(new Signal()
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

        private async Task ProduceSignals(IEnumerable<SuperTrendSSLData> data, int maxPositions)
        {
            int periodKlines = 200;

            for (uint i = 0; i < uint.MaxValue; i++)
            {
                try
                {
                    if (_runningPositions.Count < maxPositions)
                    {
                        var klines = await _trade.GetLstKlinesAsync(data.Select(x => x.Symbol), (KlineInterval)_tradeSetting.TimeFrame, periodKlines);
                        IEnumerable<Signal> signals = GetSignals(klines);

                        if (signals.Any())
                        {
                            var balanceUSDT = await _trade.GetBalanceAsync();
                            if (balanceUSDT != -1)
                            {
                                if (balanceUSDT >= _tradeSetting.BalanceUSDT)
                                {
                                    foreach (Signal signal in signals)
                                    {
                                        lock (locker)
                                        {
                                            if (_runningPositions.Count < maxPositions)
                                            {
                                                bool ckeckRunPositions = _runningPositions.Where(x => x.Symbol.Equals(signal.Symbol)).Any();
                                                if (!ckeckRunPositions)
                                                {
                                                    bool isPost = _bufferPositions.Post(signal);
                                                    if (isPost)
                                                    {
                                                        _runningPositions.Add(signal);

                                                        Console.WriteLine($"User: {_nameUser} -- добавили позицию {signal.Symbol} в список -- {DateTime.Now.ToUniversalTime()}");
                                                        Console.WriteLine($"User: {_nameUser} -- Количество активных позиций: {_runningPositions.Count}");
                                                    }
                                                }
                                            }
                                            else { Console.WriteLine($"User: {_nameUser} -- Максимальное количество открытых позиций достигнуто!"); break; }
                                        }
                                    }
                                }
                                else { Console.WriteLine($"Баланс меньше {_tradeSetting.BalanceUSDT}"); }
                            }
                            else { continue; }
                        }
                    }
                    await WaitTime();
                }
                catch (Exception ex) { Console.WriteLine(ex.Message); }
            }
        }
    }
}
