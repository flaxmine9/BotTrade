using Binance;
using Binance.Net.Enums;
using Binance.Net.Objects.Futures.FuturesData;
using Binance.Net.Objects.Futures.MarketData;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
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

        /// <summary>
        /// Проверяем новые открытые позиции
        /// </summary>
        /// <returns>Позиции</returns>
        public async Task<IEnumerable<BinancePositionDetailsUsdt>> CheckOpenPositions()
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

            IEnumerable<BinanceFuturesOrder> openCurrentOrders = await _binanceInteraction.GetCurrentOpenOrdersAsync(symbol);

            if (openCurrentOrders.Any())
            {
                for (uint i = 0; i < uint.MaxValue; i++)
                {
                    IEnumerable<BinanceFuturesOrder> newCurrentOpenOrders = await _binanceInteraction.GetCurrentOpenOrdersAsync(symbol);

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

                            bool canceledOpenOrders = await _binanceInteraction.CancelOpenOrders(stopMarketOrder.First().Symbol);
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

                            bool canceledTakeProfit = await _binanceInteraction.CancelOpenOrders(takeProfitMarketOrder.First().Symbol);
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
                                var cancelStopMarket = await _binanceInteraction.CancelOrder(currentStopMarket.First());
                                if (cancelStopMarket)
                                {
                                    Console.WriteLine($"{symbol}: Отменяем StopMarket по цене {currentStopMarket.First().StopPrice} $");
                                    // Replace StopMarket Order

                                    var replaceStopMarketOrder = await _binanceInteraction.ReplaceStopMarketOrderAsync(currentStopMarket.First(), priceLimit, _tradeSetting.StopLoss);

                                    if (replaceStopMarketOrder != null)
                                    {
                                        Console.WriteLine($"{symbol}: Переставляем StopMarket по новой цене {replaceStopMarketOrder.StopPrice} $");
                                    }
                                }
                            }
                        }

                        openCurrentOrders = await _binanceInteraction.GetCurrentOpenOrdersAsync(symbol);

                        #endregion
                    }

                    await Task.Delay(500);
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
        public GridOrder GetGridOrders(BinancePositionDetailsUsdt position, decimal takeProfit, decimal stopLoss)
        {
            BinanceFuturesUsdtSymbol symbolInfo = _binanceInteraction.GetInfo(position.Symbol);

            decimal quantitiesAsset = _binanceInteraction.CalculateQuantity(position, takeProfit);
            int quantityOrders = _binanceInteraction.CountQuantityOrders(position, quantitiesAsset);

            decimal percentBetweenOrders = _binanceInteraction.CountPercenBetweenOrders(takeProfit, quantityOrders);

            OrderSide orderSide = position.Quantity < 0 ? OrderSide.Buy : OrderSide.Sell;

            GridOrder gridOrder = new GridOrder()
            {
                ClosePositionOrders = new List<BinanceFuturesPlacedOrder>(),
                LimitOrders = new List<BinanceFuturesBatchOrder>()
            };

            decimal priceTakeProfitMarket = orderSide.Equals(OrderSide.Buy) ? position.EntryPrice / takeProfit : position.EntryPrice * takeProfit;
            decimal priceStopMarket = orderSide.Equals(OrderSide.Buy) ? position.EntryPrice * stopLoss : position.EntryPrice / stopLoss;

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

            for (short i = 1; i < quantityOrders + 1; i++)
            {
                decimal price = orderSide.Equals(OrderSide.Buy) ? position.EntryPrice / (1 + percentBetweenOrders * i) : position.EntryPrice * (1 + percentBetweenOrders * i);
                price -= price % symbolInfo.PriceFilter.TickSize;

                gridOrder.LimitOrders.Add(new BinanceFuturesBatchOrder()
                {
                    Side = orderSide,
                    Symbol = position.Symbol,
                    Type = OrderType.Limit,
                    TimeInForce = TimeInForce.GoodTillCancel,
                    Quantity = quantitiesAsset,
                    Price = price,
                    WorkingType = WorkingType.Contract
                });
            }

            return gridOrder;
        }
    }
}