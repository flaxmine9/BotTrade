using Binance.Models;
using Binance.Net;
using Binance.Net.Enums;
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

        public BinanceInteraction(string key, string secretKey, string typeNetBinance)
        {
            if (typeNetBinance.Equals("MainNet"))
            {
                _binanceClient = new BinanceClient(new BinanceClientOptions()
                {
                    ApiCredentials = new ApiCredentials(key, secretKey)
                });
            }
            else
            {
                _binanceClient = new BinanceClient(new BinanceClientOptions(BinanceApiAddresses.TestNet)
                {
                    ApiCredentials = new ApiCredentials(key, secretKey)
                });
            }
        }

        #region binance helpers

        /// <summary>
        /// Считаем минимальное количество валюты, которое можно купить/продать на 5$
        /// </summary>
        /// <param name="position">Текущая открытая позиция</param>
        /// <param name="takeProfit">Профит</param>
        /// <returns>Минимальное количество валюты за один ордер</returns>
        public OrderInfo CalculateQuantity(BinancePositionDetailsUsdt position, decimal takeProfit, int maxOrders)
        {
            OrderInfo orderInfo = new OrderInfo();

            int quantityRaspredelenieOnOrders = 3;

            var echange = GetInfo(position.Symbol);
            var quantity = echange.LotSizeFilter.MinQuantity;

            if (position.Quantity > 0)
            {
                for (uint i = 0; i < uint.MaxValue; i++)
                {
                    var gg = quantity * position.EntryPrice;
                    if (gg <= 5.05m)
                    {
                        quantity += echange.LotSizeFilter.MinQuantity;
                    }
                    else { break; }
                }
            }
            else
            {
                var priceTakeProfit = position.EntryPrice / takeProfit;

                for (uint i = 0; i < uint.MaxValue; i++)
                {
                    if (quantity * priceTakeProfit <= 5.05m)
                    {
                        quantity += echange.LotSizeFilter.MinQuantity;
                    }
                    else { break; }
                }
            }

            // всего количество ордеров
            var quantityOrders = Math.Abs(position.Quantity) / quantity;
            quantityOrders -= quantityOrders % 0.1m;

            decimal quantityForOneLimitOrder = 0.0m;
            List<decimal> quantitiesLst = new();

            // Проверяем разницу между ордерами. Разница должна быть больше, чем комиссия (0.02%)
            if (quantityOrders > maxOrders)
            {
                quantityOrders = maxOrders;
            }

            // теперь maxOrders равен количеству при разници между ордерами больше комиссии
            // quantityForOneLimitOrder количество на один лимитный ордер

            // распределяем 65% количества монет на 3 ордера, а 35% на оставишеся ордера (maxOrder - 3)
            decimal halfQuantityOfPosition = Math.Abs(position.Quantity) * 0.6m;
            halfQuantityOfPosition -= halfQuantityOfPosition % echange.LotSizeFilter.MinQuantity;

            decimal ostatolQuantityOfPosition = Math.Abs(position.Quantity) - halfQuantityOfPosition;
            ostatolQuantityOfPosition -= ostatolQuantityOfPosition % echange.LotSizeFilter.MinQuantity;

            decimal halfQuantityDelOn3Order = halfQuantityOfPosition / quantityRaspredelenieOnOrders;
            halfQuantityDelOn3Order -= halfQuantityDelOn3Order % echange.LotSizeFilter.MinQuantity;

            for (int i = 0; i < 2; i++)
            {
                if (halfQuantityDelOn3Order < quantity)
                {
                    quantityRaspredelenieOnOrders -= 1;
                    halfQuantityDelOn3Order = halfQuantityOfPosition / quantityRaspredelenieOnOrders;
                }
                else 
                {
                    break; 
                }
            }

            halfQuantityDelOn3Order -= halfQuantityDelOn3Order % echange.LotSizeFilter.MinQuantity;

            for (int i = 0; i < quantityRaspredelenieOnOrders; i++)
            {
                quantitiesLst.Add(halfQuantityDelOn3Order);
            }
            quantityOrders -= quantityRaspredelenieOnOrders;

            for (int i = (int)quantityOrders; i >= 1; i++)
            {
                if ((takeProfit - 1.0m) / quantityOrders < 0.0002m)
                {
                    quantityOrders -= 1;
                }
                else
                {
                    quantityForOneLimitOrder = ostatolQuantityOfPosition / quantityOrders;
                    quantityForOneLimitOrder -= quantityForOneLimitOrder % echange.LotSizeFilter.MinQuantity;

                    if(quantityForOneLimitOrder >= quantity)
                    {
                        break;
                    }
                    else { quantityOrders -= 1; }
                }
            }

            for (int i = 0; i < (int)quantityOrders; i++)
            {
                quantitiesLst.Add(quantityForOneLimitOrder);
            }

            return new OrderInfo() { QuantitiesAsset = quantitiesLst, QuantityOrders = quantitiesLst.Count };
        }

        public OrderInfo CalculateQuantity2(BinancePositionDetailsUsdt position, decimal takeProfit, int maxOrders)
        {
            OrderInfo orderInfo = new OrderInfo() { QuantitiesAsset = new List<decimal>() };

            var exchange = GetInfo(position.Symbol);
            var minQuantity = exchange.LotSizeFilter.MinQuantity;

            decimal quantityForOneOrder = Math.Abs(position.Quantity) / maxOrders;
            quantityForOneOrder -= quantityForOneOrder % exchange.LotSizeFilter.MinQuantity;

            if (position.Quantity > 0)
            {
                for (int i = 0; i < maxOrders; i++)
                {
                    if (quantityForOneOrder <= minQuantity
                    && quantityForOneOrder * position.EntryPrice < 5.0m)
                    {
                        maxOrders -= 1;
                        quantityForOneOrder = Math.Abs(position.Quantity) / maxOrders;
                    }
                    else { break; }
                }
            }
            else if(position.Quantity < 0)
            {
                for (int i = 0; i < maxOrders; i++)
                {
                    if (quantityForOneOrder <= minQuantity
                    && quantityForOneOrder * position.EntryPrice / takeProfit <= 5.0m)
                    {
                        maxOrders -= 1;
                        quantityForOneOrder = Math.Abs(position.Quantity) / maxOrders;
                    }
                    else { break; }
                }
            }

            for (int i = 0; i < maxOrders; i++)
            {
                orderInfo.QuantitiesAsset.Add(quantityForOneOrder);
            }

            orderInfo.QuantityOrders = maxOrders;

            return orderInfo;
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
                return (profit - 1) / quantityOrders;
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


        public async Task<IEnumerable<BinanceFuturesUsdtTrade>> GetTradeHistory(string symbol, DateTime startTime, int limit)
        {
            var result = await _binanceClient.FuturesUsdt.Order.GetUserTradesAsync(symbol, startTime, limit: limit, receiveWindow: 15000);
            if (result.Success)
            {
                return result.Data;
            }
            return new List<BinanceFuturesUsdtTrade>();
        }

        
        public async Task<IEnumerable<BinanceFuturesOrder>> GetCurrentOpenOrdersAsync(string symbol)
        {
            var openOrders = await _binanceClient.FuturesUsdt.Order.GetOpenOrdersAsync(symbol, receiveWindow: 15000);
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
            var canceled = await _binanceClient.FuturesUsdt.Order.CancelAllOrdersAsync(symbol, receiveWindow: 15000);
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

            var placedOrders = (await Task.WhenAll(batchesFive.Select(x => _binanceClient.FuturesUsdt.Order.PlaceMultipleOrdersAsync(x, receiveWindow: 15000))))
                .Where(x => x.Success)
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
            var resultOrders = (await Task.WhenAll(orders
                .Select(x => _binanceClient.FuturesUsdt.Order.PlaceOrderAsync(x.Symbol, x.Side, x.Type, quantity: x.Quantity, stopPrice: x.StopPrice, closePosition: x.ClosePosition, receiveWindow: 15000))))
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
                stopMarketOrder.Type, Math.Abs(stopMarketOrder.Quantity), stopPrice: stopPrice, closePosition: true, receiveWindow: 15000);

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
            var res = await _binanceClient.FuturesUsdt.Order.CancelOrderAsync(order.Symbol, order.OrderId, receiveWindow: 15000);
            if (res.Success)
            {
                return true;
            }
            return false;
        }

        public async Task<BinanceFuturesPlacedOrder> MarketPlaceOrderAsync(string symbol, decimal quantity, OrderSide orderSide)
        {
            var marketOrder = await _binanceClient.FuturesUsdt.Order.PlaceOrderAsync(symbol, orderSide, OrderType.Market, quantity);
            if (marketOrder.Success)
            {
                return marketOrder.Data;
            }

            return null;
        }

        #endregion


        /// <summary>
        /// Применяем настройки (плечо и тип маржи)
        /// </summary>
        /// <param name="symbol">валюта</param>
        /// <returns>Результат выполнения (true/false)</returns>
        public async Task<bool> SetSettingsAsync(string symbol, int leverage, string futureMarginType)
        {
            var initLeverage = await _binanceClient.FuturesUsdt.ChangeInitialLeverageAsync(symbol, leverage, receiveWindow: 15000);
            var marginType = await _binanceClient.FuturesUsdt.ChangeMarginTypeAsync(symbol, futureMarginType.Equals("Isolated") ? FuturesMarginType.Isolated : FuturesMarginType.Cross, receiveWindow: 15000);

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
                return position.Data.Where(x => x.EntryPrice != 0);
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
            var position = await _binanceClient.FuturesUsdt.GetPositionInformationAsync(symbol, receiveWindow: 15000);
            if (position.Success)
            {
                return position.Data.First();
            }

            return null;
        }

        /// <summary>
        /// Получение свечей
        /// </summary>
        /// <param name="symbols">Список валют</param>
        /// <param name="limit">Количество свечей</param>
        /// <returns>Список свечей</returns>
        public async Task<IEnumerable<IEnumerable<Kline>>> GetKlinesAsync(IEnumerable<string> symbols, KlineInterval klineInterval, int limit)
        {
            var klines = (await Task.WhenAll(symbols
            .Select(symbol => _binanceClient.FuturesUsdt.Market.GetKlinesAsync(symbol, klineInterval, limit: limit))))
                .Where(x => x.Success)
                .Select(x => x.Data)
                .Select((klines, numer) => klines.Select(kline => new Kline()
                {
                    Close = kline.Close,
                    High = kline.High,
                    Low = kline.Low,
                    Open= kline.Open,
                    Symbol = symbols.ToList()[numer],
                    BaseVolume = kline.BaseVolume,
                    QuoteVolume = kline.QuoteVolume,
                    TakerBuyBaseVolume = kline.TakerBuyBaseVolume,
                    TakerBuyQuoteVolume= kline.TakerBuyQuoteVolume,
                    TradeCount = kline.TradeCount,
                    CloseTime = kline.CloseTime,
                    OpenTime = kline.OpenTime
                }));

            return klines;
        }

        public async Task<Kline> GetKlineAsync(string symbol, KlineInterval klineInterval, int limit)
        {
            var klines = await _binanceClient.FuturesUsdt.Market.GetKlinesAsync(symbol, klineInterval, limit: limit);
            if (klines.Success)
            {
                var kline = klines.Data.First();

                return new Kline()
                {
                    Close = kline.Close,
                    High = kline.High,
                    Low = kline.Low,
                    Open = kline.Open,
                    Symbol = symbol,
                    BaseVolume = kline.BaseVolume,
                    QuoteVolume = kline.QuoteVolume,
                    TakerBuyBaseVolume = kline.TakerBuyBaseVolume,
                    TakerBuyQuoteVolume = kline.TakerBuyQuoteVolume,
                    TradeCount = kline.TradeCount,
                    CloseTime = kline.CloseTime,
                    OpenTime = kline.OpenTime
                };
            }

            return null;
        }

        /// <summary>
        /// Узнаем баланс фьючерсного кошелька (USDT)
        /// </summary>
        /// <returns>Баланс</returns>
        public async Task<decimal> GetBalanceAsync()
        {
            var balance = await _binanceClient.FuturesUsdt.Account.GetBalanceAsync(receiveWindow: 15000);
            if (balance.Success)
            {
                return balance.Data.Where(x => x.Asset.Equals("USDT")).First().AvailableBalance;
            }

            return -1;
        }



        #region market methods

        public async Task<decimal> GetCurrentPrice(string symbol)
        {
            var price = await _binanceClient.FuturesUsdt.Market.GetPriceAsync(symbol);
            if (price.Success)
            {
                return price.Data.Price;
            }

            return -1;
        }

        #endregion
    }
}