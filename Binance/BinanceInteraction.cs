using Binance.Net;
using Binance.Net.Enums;
using Binance.Net.Interfaces;
using Binance.Net.Objects;
using Binance.Net.Objects.Futures.FuturesData;
using Binance.Net.Objects.Futures.MarketData;
using CryptoExchange.Net.Authentication;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using TechnicalIndicator.Models;

namespace Binance
{
    public class BinanceInteraction
    {
        private BinanceClient _binanceClient { get; set; }
        public BinanceFuturesUsdtExchangeInfo _exchangeInfo { get; set; }

        public BinanceInteraction(string key, string secretKey)
        {
            _binanceClient = new BinanceClient(new BinanceClientOptions()
            {
                ApiCredentials = new ApiCredentials(key, secretKey)
            });

            #region TestNet Binance

            //_binanceClient = new BinanceClient(new BinanceClientOptions(BinanceApiAddresses.TestNet)
            //{
            //    ApiCredentials = new ApiCredentials(key, secretKey)
            //});

            #endregion
        }

        #region binance helpers

        /// <summary>
        /// Считаем минимальное количество валюты, которое можно купить/продать на 5$
        /// </summary>
        /// <param name="position">Текущая открытая позиция</param>
        /// <param name="takeProfit">Профит</param>
        /// <returns>Минимальное количество валюты за один ордер</returns>
        public decimal CalculateQuantity(BinancePositionDetailsUsdt position, decimal takeProfit, int maxOrders)
        {
            var echange = GetInfo(position.Symbol);
            var quantity = echange.LotSizeFilter.MinQuantity;

            if (position.Quantity > 0)
            {
                for (int i = 0; i < 500000; i++)
                {
                    var gg = quantity * position.EntryPrice;
                    if (gg <= 5.1m)
                    {
                        quantity += echange.LotSizeFilter.MinQuantity;
                    }
                    else { break; }
                }
            }
            else
            {
                var priceTakeProfit = position.EntryPrice / takeProfit;

                for (int i = 0; i < 500000; i++)
                {
                    if (quantity * priceTakeProfit <= 5.1m)
                    {
                        quantity += echange.LotSizeFilter.MinQuantity;
                    }
                    else { break; }
                }
            }

            var quantityOrders = Math.Abs(position.Quantity) / quantity;
            quantityOrders -= quantityOrders % 0.1m;

            if ((int)quantityOrders > maxOrders)
            {
                for (int i = maxOrders; i > 2; i--)
                {
                    if ((int)quantityOrders > i && (takeProfit - 1.0m) / i > 0.0002m)
                    {
                        var quantityRazdelNa15Order = Math.Abs(position.Quantity) / i;
                        quantityRazdelNa15Order -= quantityRazdelNa15Order % echange.LotSizeFilter.MinQuantity;

                        quantity = quantityRazdelNa15Order;
                        break;
                    }
                }
            }

            return GetNormalizeLotSize(position.Symbol, quantity);
        }

        /// <summary>
        /// Применяем условия биржи для количества валюты
        /// </summary>
        /// <param name="symbol">Валюта</param>
        /// <param name="amount">Количество</param>
        /// <returns>Нормализованное количество</returns>
        public decimal GetNormalizeLotSize(string symbol, decimal amount)
        {
            var exchangeInfo = GetInfo(symbol);

            amount -= amount % exchangeInfo.LotSizeFilter.StepSize;

            decimal quantity = amount >= exchangeInfo.LotSizeFilter.MinQuantity
                            && amount <= exchangeInfo.LotSizeFilter.MaxQuantity
                            && ((amount - exchangeInfo.LotSizeFilter.MinQuantity) % exchangeInfo.LotSizeFilter.StepSize) == 0
                            ? amount : 0;

            return quantity - quantity % exchangeInfo.LotSizeFilter.StepSize;
        }

        /// <summary>
        /// Получаем процентное отношение между ордерами
        /// </summary>
        /// <param name="profit">Профи от цена вхожа</param>
        /// <param name="quantityOrders">Количество ордеров</param>
        /// <returns>Процентое соотношение</returns>
        public decimal CountPercenBetweenOrders(decimal profit, int quantityOrders)
        {
            if (quantityOrders != 0)
            {
                var percentBetweenOrders = (profit - 1) / quantityOrders;

                return (percentBetweenOrders - percentBetweenOrders % 0.0001m);
            }

            return 0;
        }

        /// <summary>
        /// Количество ордеров
        /// </summary>
        /// <param name="position">Текущая позиция</param>
        /// <param name="quantitiesAsset">Минимальное количество валюты на один ордер</param>
        /// <returns>Количество ордеров</returns>
        public int CountQuantityOrders(BinancePositionDetailsUsdt position, decimal quantitiesAsset)
        {
            decimal quantityOrders = Math.Abs(position.Quantity) / quantitiesAsset;
            int result = (int)(quantityOrders - quantityOrders % 0.1m);

            return result;
        }


        #endregion

        #region Информация о валюте

        /// <summary>
        /// Получаем информацию о всех валютах на Binance Futures
        /// </summary>
        /// <returns></returns>
        public async Task GetExchangeInformationAsync()
        {
            var info = await _binanceClient.FuturesUsdt.System.GetExchangeInfoAsync();
            if (info.Success)
            {
                _exchangeInfo = info.Data;
            }
            else { _exchangeInfo = null; }
        }

        /// <summary>
        /// Получаем информацию о торговале по текущей валюте
        /// </summary>
        /// <param name="symbol">валюта</param>
        /// <returns>информация о валюте</returns>
        public BinanceFuturesUsdtSymbol GetInfo(string symbol)
        {
            return _exchangeInfo.Symbols.Where(x => x.Name.Equals(symbol)).FirstOrDefault();
        }

        #endregion

        #region Операции с ордерами


        /// <summary>
        /// Получаем все текущие ордера по указанной валюте
        /// </summary>
        /// <param name="symbol">Валюта</param>
        /// <returns>Ордера</returns>
        public async Task<IEnumerable<BinanceFuturesOrder>> GetCurrentOpenOrdersAsync(string symbol)
        {
            var openOrders = await _binanceClient.FuturesUsdt.Order.GetOpenOrdersAsync(symbol);
            if (openOrders.Success)
            {
                return openOrders.Data;
            }

            return new List<BinanceFuturesOrder>();
        }

        /// <summary>
        /// Отменяем все текущие открытые ордера по указанной валюте
        /// </summary>
        /// <param name="symbol">Валюта</param>
        /// <returns>Результат отмены</returns>
        public async Task<bool> CancelOpenOrders(string symbol)
        {
            var canceled = await _binanceClient.FuturesUsdt.Order.CancelAllOrdersAsync(symbol);
            if (canceled.Success)
            {
                return true;
            }
            return false;
        }

        /// <summary>
        /// Выставляем ордера
        /// </summary>
        /// <param name="orders">Список ордеров, которые необходимо выставить</param>
        /// <returns>Результат, если true - все ордера выставлены</returns>
        public async Task<IEnumerable<BinanceFuturesPlacedOrder>> PlaceBatchesAsync(IEnumerable<BinanceFuturesBatchOrder> orders)
        {
            IEnumerable<BinanceFuturesBatchOrder[]> batchesFive = orders
                .Select((x, i) => new { Index = i, Value = x })
                .GroupBy(x => x.Index / 5)
                .Select(x => x.Select(v => v.Value).ToArray());

            var placedOrders = (await Task.WhenAll(batchesFive.Select(x => _binanceClient.FuturesUsdt.Order.PlaceMultipleOrdersAsync(x))))
                .SelectMany(x => x.Data).Select(x => x.Data);

            return placedOrders;
        }

        /// <summary>
        /// Выставление ордеров (StopMarket и TakeProfitMarket)
        /// </summary>
        /// <param name="orders">ордера</param>
        /// <returns></returns>
        public async Task<IEnumerable<BinanceFuturesPlacedOrder>> PlaceClosePositionOrdersAsync(IEnumerable<BinanceFuturesPlacedOrder> orders)
        {
            var resultOrders = (await Task.WhenAll(orders.Select(x => _binanceClient.FuturesUsdt.Order.PlaceOrderAsync(x.Symbol, x.Side, x.Type, quantity: x.Quantity, stopPrice: x.StopPrice, closePosition: x.ClosePosition))))
                .Select(x => x.Data);

            return resultOrders;
        }

        /// <summary>
        /// Переставляем StopMarket ордер на велечину changePricePercent
        /// </summary>
        /// <param name="stopMarketOrder">Отмененный StopMarket ордер</param>
        /// <param name="limitPrice">Цена последнего исполненного лимитного ордера</param>
        /// <param name="changePricePercent">процент сдвига StopMarket ордера от limitPrice</param>
        /// <returns>Результат выполнения</returns>
        public async Task<BinanceFuturesPlacedOrder> ReplaceStopMarketOrderAsync(BinanceFuturesOrder stopMarketOrder, decimal limitPrice, decimal changePricePercent)
        {
            var exchange = GetInfo(stopMarketOrder.Symbol);
            if (exchange == null)
            {
                await GetExchangeInformationAsync();
                exchange = GetInfo(stopMarketOrder.Symbol);
            }

            decimal stopPrice = stopMarketOrder.Side.Equals(OrderSide.Sell) ? limitPrice / changePricePercent : limitPrice * changePricePercent;
            stopPrice -= stopPrice % exchange.PriceFilter.TickSize;

            var res = await _binanceClient.FuturesUsdt.Order.PlaceOrderAsync(stopMarketOrder.Symbol, stopMarketOrder.Side,
                stopMarketOrder.Type, Math.Abs(stopMarketOrder.Quantity), stopPrice: stopPrice, closePosition: true);

            if (res.Success)
            {
                return res.Data;
            }
            return null;
        }

        /// <summary>
        /// Отменяем ордер
        /// </summary>
        /// <param name="order">Текущий ордер</param>
        /// <returns></returns>
        public async Task<bool> CancelOrder(BinanceFuturesOrder order)
        {
            var res = await _binanceClient.FuturesUsdt.Order.CancelOrderAsync(order.Symbol, order.OrderId);
            if (res.Success)
            {
                return true;
            }
            return false;
        }

        #endregion


        /// <summary>
        /// Применяем настройки (плечо и тип маржи)
        /// </summary>
        /// <param name="symbol">валюта</param>
        /// <returns>Результат выполнения (true/false)</returns>
        public async Task<bool> SetSettingsAsync(string symbol, int leverage, string futureMarginType)
        {
            var initLeverage = await _binanceClient.FuturesUsdt.ChangeInitialLeverageAsync(symbol, leverage);
            var marginType = await _binanceClient.FuturesUsdt.ChangeMarginTypeAsync(symbol, futureMarginType.Equals("Isolated") ? FuturesMarginType.Isolated : FuturesMarginType.Cross);

            if (initLeverage.Success && marginType.Success)
            {
                return true;
            }

            return false;
        }

        /// <summary>
        /// Получаем текущие открытые позиции
        /// </summary>
        /// <param name="symbols">валютные пары</param>
        /// <returns>Список открытых позиций</returns>
        public async Task<IEnumerable<BinancePositionDetailsUsdt>> GetCurrentOpenPositionsAsync()
        {
            var position = await _binanceClient.FuturesUsdt.GetPositionInformationAsync();
            if (position.Success)
            {
                return position.Data.Where(x => x.EntryPrice != 0 && !x.Symbol.Equals("YFIIUSDT"));
            }

            return new List<BinancePositionDetailsUsdt>();
        }

        /// <summary>
        /// Получаем текущуюю открытую позицию по заданной валюе
        /// </summary>
        /// <param name="symbol">Валюта</param>
        /// <returns>Открытая позиция</returns>
        public async Task<BinancePositionDetailsUsdt> GetCurrentOpenPositionAsync(string symbol)
        {
            var position = await _binanceClient.FuturesUsdt.GetPositionInformationAsync(symbol);
            if (position.Success)
            {
                return position.Data.FirstOrDefault();
            }

            return null;
        }

        /// <summary>
        /// Получение свечей
        /// </summary>
        /// <param name="symbols">Список валют</param>
        /// <param name="limit">Количество свечей</param>
        /// <returns>Список свечей</returns>
        public async Task<IEnumerable<IEnumerable<IBinanceKline>>> GetKlinesAsync(IEnumerable<string> symbols, KlineInterval klineInterval, int limit)
        {
            var klines = (await Task.WhenAll(symbols
            .Select(symbol => _binanceClient.FuturesUsdt.Market.GetKlinesAsync(symbol, klineInterval, limit: limit))))
                .Where(x => x.Success)
                .Select(x => x.Data.SkipLast(1));

            return klines;
        }

        public async Task<IEnumerable<Kline>> GetKlineAsync(string symbol, KlineInterval klineInterval, int limit)
        {
            var klines = await _binanceClient.FuturesUsdt.Market.GetKlinesAsync(symbol, klineInterval, limit: limit);
            if (klines.Success)
            {
                return klines.Data.SkipLast(1).Select(x=> new Kline()
                {
                    Symbol = symbol,
                    Open = x.Open,
                    Low= x.Low,
                    High= x.High,
                    Close = x.Close
                });
            }

            return new List<Kline>();
        }
    }
}