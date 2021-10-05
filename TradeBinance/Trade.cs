using Binance;
using Binance.Models;
using Binance.Net.Enums;
using Binance.Net.Interfaces;
using Binance.Net.Objects.Futures.FuturesData;
using Binance.Net.Objects.Futures.MarketData;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using TechnicalIndicator.Models;
using TradeBinance.Equalities;
using TradeBinance.Models;

namespace TradeBinance
{
    public class Trade
    {
        private EqualityOrder _equalityOrder { get; set; }
        private BinanceInteraction _binanceInteraction { get; set; }
        private TradeSetting _tradeSetting { get; set; }

        public Trade(string key, string secretKey, TradeSetting tradeSetting)
        {
            _binanceInteraction = new BinanceInteraction(key, secretKey);

            _equalityOrder = new EqualityOrder();
            _tradeSetting = tradeSetting;
        }

        public async Task SetExchangeInformationAsync()
        {
            await _binanceInteraction.GetExchangeInformationAsync();
        }

        public async Task PlaceOrders(GridOrder gridOrders)
        {
            var placedLimitOrders = await _binanceInteraction.PlaceBatchesAsync(gridOrders.LimitOrders);
            var placedStopOrders = await _binanceInteraction.PlaceClosePositionOrdersAsync(gridOrders.ClosePositionOrders);
        }

        public async Task SetTradeSettings(IEnumerable<string> symbols)
        {
            await Task.WhenAll(symbols.Select(s => _binanceInteraction.SetSettingsAsync(s, _tradeSetting.Leverage, _tradeSetting.FuturesMarginType)));
        }

        /// <summary>
        /// Получаем открытую позицию
        /// </summary>
        /// <param name="symbol">валюта</param>
        /// <returns></returns>
        public async Task<BinancePositionDetailsUsdt> GetCurrentOpenPositionAsync(string symbol)
        {
            return await _binanceInteraction.GetCurrentOpenPositionAsync(symbol);
        }


        /// <summary>
        /// Получаем открытую позицию
        /// </summary>
        /// <param name="symbol">валюта</param>
        /// <returns></returns>
        public async Task<IEnumerable<BinancePositionDetailsUsdt>> GetCurrentOpenPositionsAsync()
        {
            return await _binanceInteraction.GetCurrentOpenPositionsAsync();
        }

        public async Task<IEnumerable<BinanceFuturesOrder>> GetOpenOrders(string symbol)
        {
            return await _binanceInteraction.GetCurrentOpenOrdersAsync(symbol);
        }

        /// <summary>
        /// Контролируем текущие открытые ордера
        /// </summary>
        /// <param name="positions">Текущая открытая позиция</param>
        /// <returns></returns>
        public async Task ControlOrders(string symbol)
        {
            Console.WriteLine($"{symbol}: Контролируем ордера");

            IEnumerable<BinanceFuturesOrder> openCurrentOrders = await _binanceInteraction.GetCurrentOpenOrdersAsync(symbol).ConfigureAwait(false);

            if (openCurrentOrders.Any())
            {
                for (uint i = 0; i < uint.MaxValue; i++)
                {
                    IEnumerable<BinanceFuturesOrder> newCurrentOpenOrders = await _binanceInteraction.GetCurrentOpenOrdersAsync(symbol).ConfigureAwait(false);
                    if (newCurrentOpenOrders.Any())
                    {
                        IEnumerable<BinanceFuturesOrder> exceptedOrders = openCurrentOrders.Except<BinanceFuturesOrder>(newCurrentOpenOrders, _equalityOrder);

                        if (exceptedOrders.Any())
                        {
                            #region Выбираем Limit, StopMarket и TakeProfitMarket ордера

                            IEnumerable<BinanceFuturesOrder> stopMarketOrder = exceptedOrders.Where(x => x.Type.Equals(OrderType.StopMarket));
                            IEnumerable<BinanceFuturesOrder> takeProfitMarketOrder = exceptedOrders.Where(x => x.Type.Equals(OrderType.TakeProfitMarket));
                            IEnumerable<BinanceFuturesOrder> limitOrders = exceptedOrders.Where(x => x.Type.Equals(OrderType.Limit));

                            #endregion

                            #region Проверяем ордера

                            if (stopMarketOrder.Any())
                            {
                                Console.WriteLine($"{symbol}: StopMarket Order выполнился");
                                Console.WriteLine($"{symbol}: Отменяем все оставшиеся ордера");

                                bool canceledOpenOrders = await _binanceInteraction.CancelOpenOrders(stopMarketOrder.First().Symbol).ConfigureAwait(false);
                                if (canceledOpenOrders)
                                {
                                    Console.WriteLine($"{symbol}: Отменили все оставшиеся ордера");
                                    break;
                                }
                                else { Console.WriteLine($"{symbol}: Не удалось отменить открытые ордера по валюте {stopMarketOrder.First().Symbol}"); continue; }

                            }
                            else if (takeProfitMarketOrder.Any())
                            {
                                Console.WriteLine($"{symbol}: ProfitMarket Order выполнился");
                                Console.WriteLine($"{symbol}: Отменяем все оставшиеся ордера");

                                bool canceledTakeProfit = await _binanceInteraction.CancelOpenOrders(takeProfitMarketOrder.First().Symbol).ConfigureAwait(false);
                                if (canceledTakeProfit)
                                {
                                    Console.WriteLine($"{symbol}: Отменили все оставшиеся ордера");
                                    break;
                                }
                                else { Console.WriteLine($"{symbol}: Не удалось отменить открытые ордера по валюте {takeProfitMarketOrder.First().Symbol}"); continue; }
                            }
                            else if (limitOrders.Any())
                            {
                                string symbolLimit = limitOrders.First().Symbol;
                                decimal priceLimit = limitOrders.First().Quantity > 0 ? limitOrders.Max(p => p.Price) : limitOrders.Min(p => p.Price);

                                var currentStopMarket = newCurrentOpenOrders.Where(x => x.Type.Equals(OrderType.StopMarket) && x.Symbol.Equals(symbolLimit));

                                if (newCurrentOpenOrders.Any())
                                {
                                    var cancelStopMarket = await _binanceInteraction.CancelOrder(currentStopMarket.First()).ConfigureAwait(false);
                                    if (cancelStopMarket)
                                    {
                                        Console.WriteLine($"{symbol}: Отменяем StopMarket по цене {currentStopMarket.First().StopPrice} $");
                                        // Replace StopMarket Order

                                        var replaceStopMarketOrder = await _binanceInteraction.ReplaceStopMarketOrderAsync(currentStopMarket.First(), priceLimit, _tradeSetting.StopLoss).ConfigureAwait(false);

                                        if (replaceStopMarketOrder != null)
                                        {
                                            Console.WriteLine($"{symbol}: Переставляем StopMarket по новой цене {replaceStopMarketOrder.StopPrice} $");
                                        }
                                    }
                                }
                            }

                            openCurrentOrders = await _binanceInteraction.GetCurrentOpenOrdersAsync(symbol).ConfigureAwait(false);

                            #endregion
                        }

                    }
                }
            }
        }
       
        /// <summary>
        /// Формируем ордера (limit orders, takeProfit market and stopMarket order)
        /// </summary>
        /// <param name="position">Текщая позиция</param>
        /// <param name="takeProfit">Профит</param>
        /// <param name="stopLoss">Стоп лосс</param>
        /// <returns>Ордера</returns>
        public GridOrder GetGridOrders(BinancePositionDetailsUsdt position)
        {
            BinanceFuturesUsdtSymbol symbolInfo = _binanceInteraction.GetInfo(position.Symbol);

            OrderInfo orderInfo = _binanceInteraction.CalculateQuantity(position, _tradeSetting.TakeProfit, _tradeSetting.MaxOrders);

            decimal percentBetweenOrders = _binanceInteraction.CountPercenBetweenOrders(_tradeSetting.TakeProfit, orderInfo.QuantityOrders);

            OrderSide orderSide = position.Quantity < 0 ? OrderSide.Buy : OrderSide.Sell;

            GridOrder gridOrder = new GridOrder()
            {
                ClosePositionOrders = new List<BinanceFuturesPlacedOrder>(),
                LimitOrders = new List<BinanceFuturesBatchOrder>()
            };

            decimal priceTakeProfitMarket = orderSide.Equals(OrderSide.Buy) ? position.EntryPrice / _tradeSetting.TakeProfit : position.EntryPrice * _tradeSetting.TakeProfit;
            decimal priceStopMarket = orderSide.Equals(OrderSide.Buy) ? position.EntryPrice * _tradeSetting.StopLoss : position.EntryPrice / _tradeSetting.StopLoss;

            gridOrder.ClosePositionOrders.AddRange(new List<BinanceFuturesPlacedOrder>()
            {
                new BinanceFuturesPlacedOrder()
                {
                    Symbol = position.Symbol,
                    ClosePosition = true,
                    Side = orderSide,
                    Type = OrderType.TakeProfitMarket,
                    StopPrice = priceTakeProfitMarket - priceTakeProfitMarket % symbolInfo.PriceFilter.TickSize,
                    WorkingType = WorkingType.Contract
                },
                new BinanceFuturesPlacedOrder()
                {
                    Symbol = position.Symbol,
                    ClosePosition = true,
                    Side = orderSide,
                    Type = OrderType.StopMarket,
                    StopPrice = priceStopMarket - priceStopMarket % symbolInfo.PriceFilter.TickSize,
                    WorkingType = WorkingType.Contract
                }
            });

            for (short i = 1; i < orderInfo.QuantityOrders + 1; i++)
            {
                decimal price = orderSide.Equals(OrderSide.Buy) ? position.EntryPrice / (1 + percentBetweenOrders * i) : position.EntryPrice * (1 + percentBetweenOrders * i);
                price -= price % symbolInfo.PriceFilter.TickSize;

                gridOrder.LimitOrders.Add(new BinanceFuturesBatchOrder()
                {
                    Side = orderSide,
                    Symbol = position.Symbol,
                    Type = OrderType.Limit,
                    TimeInForce = TimeInForce.GoodTillCancel,
                    Quantity = orderInfo.QuantityAsset,
                    Price = price,
                    WorkingType = WorkingType.Contract
                });
            }

            return gridOrder;
        }

        public async Task<IEnumerable<Kline>> GetKlinesAsync(string symbol, int limit)
        {
            var result = await _binanceInteraction.GetKlineAsync(symbol, KlineInterval.FiveMinutes, limit: limit);

            return result.Select(x => new Kline()
            {
                Close = x.Close,
                High = x.High,
                Low = x.Low,
                Open = x.Open
            });
        }

        public async Task<IEnumerable<IEnumerable<Kline>>> GetLstKlinesAsync(IEnumerable<string> symbols, int limit)
        {
            return await _binanceInteraction.GetKlinesAsync(symbols, KlineInterval.FiveMinutes, limit: limit);
        }

        public async Task<bool> EntryMarket(string symbol, decimal partOfBalance, TypePosition typePosition)
        {
            var info = _binanceInteraction.GetInfo(symbol);

            var taskBalanceUSDT = _binanceInteraction.GetBalanceAsync();
            var taskCurrentPrice = _binanceInteraction.GetCurrentPrice(symbol);

            decimal balanceUSDT = await taskBalanceUSDT;
            decimal currentPrice = await taskCurrentPrice;

            if(balanceUSDT > 0 && currentPrice > 0)
            {
                decimal quantityAsset = (balanceUSDT * partOfBalance * _tradeSetting.Leverage) / currentPrice;
                quantityAsset -= quantityAsset % info.LotSizeFilter.StepSize;

                var placeMarketOrder = await _binanceInteraction.MarketPlaceOrderAsync(symbol, quantityAsset, typePosition.Equals(TypePosition.Long) ? OrderSide.Buy : OrderSide.Sell);
                if (placeMarketOrder != null)
                {
                    return true;
                }
            }

            return false;
        }
    }
}