using Binance.Net.Enums;
using Binance.Net.Objects.Futures.FuturesData;
using DataBase;
using DataBase.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Threading.Tasks.Dataflow;
using TradeBinance;
using TradeBinance.Models;

namespace TradePipeLine
{
    public class PipeLine
    {
        private Trade _trade { get; set; }
        private TradeSetting _tradeSetting { get; set; }
        private ApplicationContext _dataBase { get; set; }
        private User _user { get; set; }

        #region buffers

        private BufferBlock<string> _bufferFailedPosition { get; set; }
        private BufferBlock<TradeSignal> _bufferFailedEntryMarket { get; set; }
        private BufferBlock<GridOrder> _bufferFailedPlaceOrders { get; set; }
        private BufferBlock<BinanceFuturesOrder> _bufferFirstFinishedOrder { get; set; }
        private BufferBlock<IEnumerable<BinanceFuturesUsdtTrade>> _bufferWriteTradeHistoryToDB { get; set; }
        private BufferBlock<TradeSignal> _bufferPositions { get; set; }
        private List<TradeSignal> _runningPositions { get; set; }

        #endregion


        private readonly object locker = new object();
        private bool _waitAfterExitPosition { get; set; } = false;
        private string _nameStrategy { get; set; }
        private int _delayMilliseconds { get; set; }

        public PipeLine(Trade trade, User user, ApplicationContext db, string nameStrategy, bool waitAfterExitPosition, int delayMilliseconds)
        {
            _bufferFailedPosition = new();
            _bufferFailedEntryMarket = new();
            _bufferFailedPlaceOrders = new();
            _bufferPositions = new();

            _bufferFirstFinishedOrder = new();
            _bufferWriteTradeHistoryToDB = new BufferBlock<IEnumerable<BinanceFuturesUsdtTrade>>();

            _runningPositions = new();

            _trade = trade;
            _tradeSetting = _trade._tradeSetting;

            _dataBase = db;
            _user = user;

            _nameStrategy = nameStrategy;
            _waitAfterExitPosition = waitAfterExitPosition;
            _delayMilliseconds = delayMilliseconds;
        }

        public void AddSignal(TradeSignal tradeSignal)
        {
            lock (locker)
            {
                bool ckeckRunPositions = _runningPositions.Where(x => x.Symbol.Equals(tradeSignal.Symbol)).Any();
                if (!ckeckRunPositions)
                {
                    bool isPost = _bufferPositions.Post(tradeSignal);
                    if (isPost)
                    {
                        _runningPositions.Add(tradeSignal);

                        Console.WriteLine($"User: { _user.Name } -- добавили позицию { tradeSignal.Symbol } в список -- { DateTime.Now.ToUniversalTime() }");
                        Console.WriteLine($"User: { _user.Name } -- Количество активных позиций: {_runningPositions.Count}");
                    }
                }
            }
        }

        public List<string> GetRunningPositions()
        {
            lock (locker)
            {
                if (_runningPositions.Any())
                {
                    return _runningPositions.Select(x => x.Symbol).ToList();
                }
            }

            return new List<string>() { };
        }

        public bool CheckFreePositions()
        {
            bool check = true;

            lock (locker)
            {
                check = _runningPositions.Count < _tradeSetting.MaxPositions;
            }

            return check;
        }

        public void Create()
        {
            var options = new DataflowLinkOptions() { PropagateCompletion = true };

            #region pipeline

            var entryMarket = new TransformBlock<TradeSignal, string>(async TradeSignal =>
            {
                string entriedMarket = await _trade.EntryMarket(TradeSignal.Symbol, price: TradeSignal.Price, _tradeSetting.BalanceUSDT, TradeSignal.TypePosition);
                if (entriedMarket != null)
                {
                    Console.WriteLine($"User: {_user.Name} -- Зашли по рынку по монете {TradeSignal.Symbol}");

                    return entriedMarket;
                }
                else
                {
                    await _bufferFailedEntryMarket.SendAsync(TradeSignal);
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
                    Console.WriteLine($"User: {_user.Name} -- Получили позицию по монете {symbol}");
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
                    Console.WriteLine($"User: {_user.Name} -- Ошибка в блоке gridOrders\n" +
                        $"{ex.Message}");

                    return null;
                }
            }, new ExecutionDataflowBlockOptions() { EnsureOrdered = false });

            var placeOrders = new TransformBlock<GridOrder, IEnumerable<BinanceFuturesPlacedOrder>>(async gridOrders =>
            {
                List<BinanceFuturesPlacedOrder> lst = new();

                try
                {
                    IEnumerable<BinanceFuturesPlacedOrder> placedOrders = (await _trade.PlaceOrders(gridOrders)).Where(x => x != null);
                    if (placedOrders.Any())
                    {
                        lst.AddRange(placedOrders);

                        Console.WriteLine($"User: {_user.Name} -- Поставили ордера по монете {lst.First().Symbol}");
                    }
                    else
                    {
                        Console.WriteLine($"Валюта: {gridOrders.ClosePositionOrders.First().Symbol} -- отправляем ордера в failed block");

                        if (await _bufferFailedPlaceOrders.SendAsync(gridOrders))
                        {
                            Console.WriteLine("User: {_user.Name} -- Отправили ордера в failed block");
                        }
                    }

                    return lst;
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"User: {_user.Name} -- Ошибка в блоке placeOrders\n" +
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
                    List<BinanceFuturesOrder> finishedOrders = (await _trade.ControlOrders(placedOrders, _delayMilliseconds)).ToList();
                    if (finishedOrders.Any())
                    {
                        Console.WriteLine($"User: {_user.Name} -- выполнились ордера по валюте: {finishedOrders.First().Symbol}");
                        var minTimeOrder = finishedOrders.OrderBy(x => x.UpdateTime).First();

                        if (await _bufferFirstFinishedOrder.SendAsync(minTimeOrder))
                        {
                            Console.WriteLine($"User: {_user.Name} -- Передали ордера в буфер");
                        }

                        return finishedOrders.First().Symbol;
                    }

                    return "";
                }
                catch (Exception ex)
                {
                    Console.WriteLine("User: {_user.Name} -- Ошибка в блоке controllOrders\n" +
                        ex.Message);

                    return "";
                }

            }, new ExecutionDataflowBlockOptions { MaxDegreeOfParallelism = DataflowBlockOptions.Unbounded, EnsureOrdered = false });


            var getTradeHistoryOrders = new ActionBlock<BinanceFuturesOrder>(async _ =>
            {
                IEnumerable<BinanceFuturesUsdtTrade> historyTrades = await _trade.GetTradeHistory(_.Symbol, _.UpdateTime, limit: _tradeSetting.MaxOrders * 5);
                if (!historyTrades.Any())
                {
                    Console.WriteLine($"User: {_user.Name} -- не удалось получить историю ордерова по валюте {_.Symbol}");
                }
                else
                {
                    if (await _bufferWriteTradeHistoryToDB.SendAsync(historyTrades))
                    {
                        Console.WriteLine($"User: {_user.Name} -- передали историю ордеров по валюте {historyTrades.First().Symbol}");
                    }
                }

            }, new ExecutionDataflowBlockOptions { MaxDegreeOfParallelism = DataflowBlockOptions.Unbounded, EnsureOrdered = false });


            var writeTradeHistoryToDB = new ActionBlock<IEnumerable<BinanceFuturesUsdtTrade>>(async _ =>
            {
                Console.WriteLine($"User: {_user.Name} -- получили историю ордеров по валюте {_.First().Symbol}");

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

                    Console.WriteLine($"User: {_user.Name} -- записали выполненные ордера в базу данных по валюте {_.First().Symbol}");

                }
                catch (Exception ex)
                {
                    Console.WriteLine($"User: {_user.Name} -- Ошибка в блоке writeTradeHistoryToDB\n" +
                        $"{ex.Message}");
                }

            }, new ExecutionDataflowBlockOptions { MaxDegreeOfParallelism = 1 });


            var finishedPosition = new ActionBlock<string>(async symbol =>
            {
                if (_waitAfterExitPosition)
                {
                    var kline = await _trade.GetKlineAsync(symbol, (KlineInterval)_tradeSetting.TimeFrame, limit: 1);
                    if(kline!= null)
                    {
                        DateTime timeNow = DateTime.Now.ToUniversalTime();
                        TimeSpan waitTime = kline.CloseTime.AddMilliseconds(1050) - timeNow;

                        Console.WriteLine($"User: {_user.Name}\n" +
                            $"Стратегия: {_nameStrategy}\n" +
                            $"Ждем завершение свечи {kline.Symbol}!");

                        await Task.Delay(Math.Abs((int)waitTime.TotalMilliseconds));

                        DeletePosition(symbol);
                    }
                }
                else { DeletePosition(symbol); }
                
            }, new ExecutionDataflowBlockOptions { MaxDegreeOfParallelism = DataflowBlockOptions.Unbounded, EnsureOrdered = false });

            #endregion

            #region failed

            var tryExecuteFailedPosition = new TransformBlock<string, BinancePositionDetailsUsdt>(async symbol =>
            {
                BinancePositionDetailsUsdt position = null;

                Console.WriteLine($"User: {_user.Name} -- Валюта: {symbol} -- Пытаем получить 5 раз текущую позицию");

                for (int i = 0; i < 5; i++)
                {
                    var resultPosition = await _trade.GetCurrentOpenPositionAsync(symbol);
                    if (resultPosition != null)
                    {
                        Console.WriteLine($"User: {_user.Name} -- Валюта: {symbol} -- Получили failed position");
                        position = resultPosition;

                        break;
                    }
                }

                Console.WriteLine($"User: {_user.Name} -- Валюта: {symbol} -- Не удалось получить текущую позицию за 5 попыток");

                var result = await _trade.ClosePosition(symbol);
                if (result)
                {
                    Console.WriteLine($"User: {_user.Name} -- Закрыли позицию по валюте {symbol} после попыток получения позиции!");
                    DeletePosition(symbol);
                }
                else
                {
                    Console.WriteLine($"User: {_user.Name} -- Не удалось закрыть позицию по валюте {symbol} после попыток получения позиции!");
                }

                return position;

            }, new ExecutionDataflowBlockOptions
            {
                MaxDegreeOfParallelism = DataflowBlockOptions.Unbounded,
                EnsureOrdered = false
            });


            var tryExecuteFailedPlaceOrders = new TransformBlock<GridOrder, IEnumerable<BinanceFuturesPlacedOrder>>(async gridOrders =>
            {
                List<BinanceFuturesPlacedOrder> lst = new();
                try
                {
                    Console.WriteLine($"User: {_user.Name} -- Валюта: {gridOrders.ClosePositionOrders.First().Symbol} -- Пытаемся поставить ордера 5 раз");
                    for (int i = 0; i < 5; i++)
                    {
                        IEnumerable<BinanceFuturesPlacedOrder> placedOrders = await _trade.PlaceOrders(gridOrders);
                        if (placedOrders.Any())
                        {
                            Console.WriteLine($"User: {_user.Name} -- Валюта: {placedOrders.First().Symbol} -- Поставили failed orders");

                            return placedOrders;
                        }
                    }

                    Console.WriteLine($"User: {_user.Name} -- Валюта: {gridOrders.ClosePositionOrders.First().Symbol} -- Не удалось выставить ордера за 5 попыток");

                    var result = await _trade.ClosePosition(gridOrders.ClosePositionOrders.First().Symbol);
                    if (result)
                    {
                        Console.WriteLine($"User: {_user.Name} -- Закрыли позицию по валюте {gridOrders.ClosePositionOrders.First().Symbol} после попыток выставления ордеров!");
                        DeletePosition(gridOrders.ClosePositionOrders.First().Symbol);
                    }
                    else
                    {
                        Console.WriteLine($"User: {_user.Name} -- Не удалось закрыть позицию по валюте {gridOrders.ClosePositionOrders.First().Symbol} после попыток выставления ордеров!");
                    }

                    return lst;
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"User: {_user.Name} -- Ошибка в блоке tryExecuteFailedPlaceOrders, {ex.Message}");

                    return lst;
                }

            }, new ExecutionDataflowBlockOptions
            {
                MaxDegreeOfParallelism = DataflowBlockOptions.Unbounded,
                EnsureOrdered = false
            });

            var tryExecuteFailedEntryMarket = new TransformBlock<TradeSignal, string>(async TradeSignal =>
            {
                Console.WriteLine($"User: {_user.Name} -- Валюта: {TradeSignal.Symbol} -- Пытаемся зайти в рынок 5 раз");
                for (int i = 0; i < 5; i++)
                {
                    string entriedMarket = await _trade.EntryMarket(TradeSignal.Symbol, price: TradeSignal.Price, _tradeSetting.BalanceUSDT, TradeSignal.TypePosition);
                    if (entriedMarket != null)
                    {
                        Console.WriteLine($"User: {_user.Name} -- Валюта: {TradeSignal.Symbol} -- Зашли в failed entry market");

                        return entriedMarket;
                    }
                }

                Console.WriteLine($"User: {_user.Name} -- Валюта: {TradeSignal.Symbol} -- Не удалось зайти в рынок за 5 попыток");

                DeletePosition(TradeSignal.Symbol);

                return null;

            }, new ExecutionDataflowBlockOptions
            {
                MaxDegreeOfParallelism = DataflowBlockOptions.Unbounded,
                EnsureOrdered = false
            });

            #endregion

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
                        $"Осталось активных позииций {_user.Name}: {_runningPositions.Count}\n" +
                        $"Время: {DateTime.Now.ToUniversalTime()}");
                }
            }
        }
    }
}
