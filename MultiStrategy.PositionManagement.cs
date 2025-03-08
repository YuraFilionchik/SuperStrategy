namespace SuperStrategy
{
    using System;
    using StockSharp.Messages;
    using StockSharp.BusinessEntities;
    using DevExpress.XtraSpellChecker.Parser;

    public partial class MultiStrategy
    {
        // Флаги и статусы
        private bool _isPositionOpened = false;
        private bool _isTrailingStopActivated = false;
        private decimal _currentStopLoss = 0;
        private decimal _currentTrailingStop = 0;
        private DateTimeOffset _positionOpenTime;

        /// <summary>
        /// Открытие позиции
        /// </summary>
        private void OpenPosition(Sides side, decimal price)
        {
            try
            {
                if (price == 0)
                {
                    LogError("Нулевое значение цены");
                    return;
                }
                // Если уже есть открытая позиция, выходим
                if (_isPositionOpened)
                    return;

                // Расчет параметров позиции
                decimal atr = _currentAtr;
                if (atr == 0)
                    atr = price * 0.01m; // Значение по умолчанию, если ATR не рассчитан

                // Расчет уровней стоп-лосса и тейк-профита
                decimal stopLossPrice = side == Sides.Buy
                    ? price - (atr * StopLossMultiplier)
                    : price + (atr * StopLossMultiplier);

                decimal takeProfitPrice = side == Sides.Buy
                    ? price + (atr * TakeProfitMultiplier3)
                    : price - (atr * TakeProfitMultiplier3);

                // Расчет объема позиции
                decimal volume = TradeVolume / price;
                LogInfo($"Portfolio={Portfolio?.CurrentValue}{Portfolio?.Currency}, RiskPerTrade={RiskPerTrade * 100}%");
                decimal riskAmountUSD = TradeVolume * RiskPerTrade;

                // Если включено управление рисками, рассчитываем объем на основе риска
                if (RiskPerTrade > 0 && Portfolio != null && Portfolio.CurrentValue > 0)
                {
                    riskAmountUSD = (decimal)Portfolio.CurrentValue * RiskPerTrade;
                    decimal riskAmountContracts = riskAmountUSD / price;
                    decimal priceRisk = side == Sides.Buy ? (price - stopLossPrice) : (stopLossPrice - price);
                    if (priceRisk > 0)
                    {
                        decimal riskBasedVolume = riskAmountContracts / priceRisk;
                        // Используем меньшее из двух значений, чтобы не превысить риск
                        volume = Math.Min(volume, riskBasedVolume);
                    }
                    LogInfo($"StopLoss={(100 * priceRisk / price).ToString("N2")}%");
                }

                // Округление объема до шага объема инструмента
                var volumeStep = Security.VolumeStep ?? 0.01m;
                volume = Math.Floor(volume / volumeStep) * volumeStep;

                // Проверка минимального объема
                if (volume < volumeStep || volume <= 0)
                    volume = volumeStep;

                LogInfo($"Risk amount={riskAmountUSD}{Portfolio?.Currency}, position volume={(volume * price).ToString("N2")}");

                // Логирование уровней
                LogLevels(price, stopLossPrice, takeProfitPrice);

                // Создание рыночного ордера
                if (side == Sides.Buy)
                    BuyMarket(volume);
                else
                    SellMarket(volume);

                // Обновление состояния стратегии
                _isTrailingStopActivated = false;
                _currentStopLoss = stopLossPrice;
                _currentTrailingStop = stopLossPrice;
                _positionOpenTime = CurrentTime;

                // Использование встроенной защиты StockSharp
                // Обратите внимание на правильную передачу параметров
                StartProtection(
                    takeProfit: new Unit(Math.Abs(takeProfitPrice - price), UnitTypes.Absolute),
                    stopLoss: new Unit(Math.Abs(stopLossPrice - price), UnitTypes.Absolute),
                    isStopTrailing: true,
                    useMarketOrders: true
                );
            }
            catch (Exception ex)
            {
                LogErrorDetailed("Ошибка при открытии позиции", ex);
            }
        }

        /// <summary>
        /// Управление позицией
        /// </summary>
        private void ManagePosition(ICandleMessage candle)
        {
            try
            {
                // Получение текущей цены
                decimal currentPrice = candle.ClosePrice;

                //Если позиции нет, сбрасываем состояние
                decimal position = GetCurrentPosition();

                if (position == 0)
                {
                    _isPositionOpened = false;
                    return;
                }

                LogInfo($"ManagePosition/ position = {position}");
                // Определение направления позиции
                Sides positionSide = position > 0 ? Sides.Buy : Sides.Sell;

                // Проверка временного выхода (максимальное время в сделке)
                if ((CurrentTime - _positionOpenTime).TotalMinutes > 60)
                {
                    ClosePosition(positionSide, currentPrice, "Превышено максимальное время в сделке (60 минут)");
                    return;
                }

                // Проверка временного выхода для половины позиции (30 минут)
                if ((CurrentTime - _positionOpenTime).TotalMinutes > 30 && Math.Abs(position) == TradeVolume)
                {
                    ClosePartialPosition(positionSide, currentPrice, 0.5m, "Половина позиции закрыта по времени (30 минут)");
                }

                // Проверка срабатывания стоп-лосса
                bool isStopLossTriggered = positionSide == Sides.Buy
                    ? currentPrice <= _currentStopLoss
                    : currentPrice >= _currentStopLoss;

                if (isStopLossTriggered)
                {
                    ClosePosition(positionSide, currentPrice, "Сработал Stop-Loss");
                    return;
                }

                // Проверка срабатывания тейк-профитов и другой логики управления позицией
                CheckTakeProfitLevels(positionSide, currentPrice, candle);

                // Проверка сигналов разворота
                if (CheckReverseSignals(candle, positionSide))
                {
                    ClosePosition(positionSide, currentPrice, "Сигнал разворота");
                }
            }
            catch (Exception ex)
            {
                LogErrorDetailed("Ошибка при управлении позицией", ex);
            }
        }

        /// <summary>
        /// Проверка достижения уровней тейк-профита
        /// </summary>
        private void CheckTakeProfitLevels(Sides positionSide, decimal currentPrice, ICandleMessage candle)
        {
            try
            {
                var position = GetCurrentPosition();
                // Используем первую сделку для определения цены входа или текущую цену
                var trades = MyTrades.ToArray();
                decimal entryPrice = trades.Length > 0 ? trades[0].Trade.Price : currentPrice;

                // Расчет уровней тейк-профита
                decimal atr = _currentAtr > 0 ? _currentAtr : currentPrice * 0.01m;

                decimal takeProfit1 = positionSide == Sides.Buy
                    ? entryPrice + (atr * TakeProfitMultiplier1)
                    : entryPrice - (atr * TakeProfitMultiplier1);

                decimal takeProfit2 = positionSide == Sides.Buy
                    ? entryPrice + (atr * TakeProfitMultiplier2)
                    : entryPrice - (atr * TakeProfitMultiplier2);

                decimal takeProfit3 = positionSide == Sides.Buy
                    ? entryPrice + (atr * TakeProfitMultiplier3)
                    : entryPrice - (atr * TakeProfitMultiplier3);

                // Расчет процента текущего объема от начального
                decimal currentVolumePercent = Math.Abs(position) / TradeVolume;

                // Проверка TP1 (30% позиции)
                if (currentVolumePercent > 0.7m &&
                    (positionSide == Sides.Buy ? currentPrice >= takeProfit1 : currentPrice <= takeProfit1))
                {
                    ClosePartialPosition(positionSide, currentPrice, 0.3m, "Достигнут TP1");

                    // Активация трейлинг-стопа после TP1
                    if (!_isTrailingStopActivated)
                    {
                        _isTrailingStopActivated = true;

                        // Установка трейлинг-стопа после TP1
                        _currentTrailingStop = positionSide == Sides.Buy
                            ? currentPrice - (atr * TrailingStopMultiplier)
                            : currentPrice + (atr * TrailingStopMultiplier);

                        LogInfo($"Трейлинг-стоп активирован: {_currentTrailingStop:F8}");
                    }
                }
                // Проверка TP2 (30% позиции)
                else if (currentVolumePercent > 0.4m && currentVolumePercent <= 0.7m &&
                        (positionSide == Sides.Buy ? currentPrice >= takeProfit2 : currentPrice <= takeProfit2))
                {
                    ClosePartialPosition(positionSide, currentPrice, 0.3m / currentVolumePercent, "Достигнут TP2");
                }
                // Проверка TP3 (40% позиции)
                else if (currentVolumePercent <= 0.4m &&
                        (positionSide == Sides.Buy ? currentPrice >= takeProfit3 : currentPrice <= takeProfit3))
                {
                    ClosePosition(positionSide, currentPrice, "Достигнут TP3");
                    return;
                }

                // Проверка и обновление трейлинг-стопа
                if (_isTrailingStopActivated)
                {
                    UpdateTrailingStop(positionSide, currentPrice, atr);
                }
            }
            catch (Exception ex)
            {
                LogErrorDetailed("Ошибка при проверке уровней тейк-профита", ex);
            }
        }

        /// <summary>
        /// Обновление трейлинг-стопа
        /// </summary>
        private void UpdateTrailingStop(Sides positionSide, decimal currentPrice, decimal atr)
        {
            try
            {
                // Проверка срабатывания трейлинг-стопа
                bool isTrailingStopTriggered = positionSide == Sides.Buy
                    ? currentPrice <= _currentTrailingStop
                    : currentPrice >= _currentTrailingStop;

                if (isTrailingStopTriggered)
                {
                    ClosePosition(positionSide, currentPrice, "Сработал трейлинг-стоп");
                    return;
                }

                // Обновление трейлинг-стопа
                if (positionSide == Sides.Buy &&
                    currentPrice - (atr * TrailingStopMultiplier) > _currentTrailingStop + (atr * TrailingStopStep))
                {
                    _currentTrailingStop = currentPrice - (atr * TrailingStopMultiplier);
                    LogInfo($"Трейлинг-стоп обновлен: {_currentTrailingStop:F8}");
                }
                else if (positionSide == Sides.Sell &&
                        currentPrice + (atr * TrailingStopMultiplier) < _currentTrailingStop - (atr * TrailingStopStep))
                {
                    _currentTrailingStop = currentPrice + (atr * TrailingStopMultiplier);
                    LogInfo($"Трейлинг-стоп обновлен: {_currentTrailingStop:F8}");
                }
            }
            catch (Exception ex)
            {
                LogErrorDetailed("Ошибка при обновлении трейлинг-стопа", ex);
            }
        }

        /// <summary>
        /// Закрытие частичной позиции
        /// </summary>
        private void ClosePartialPosition(Sides positionSide, decimal price, decimal part, string reason)
        {
            try
            {
                var position = GetCurrentPosition();
                // Если позиции нет, выходим
                if (position == 0)
                    return;

                // Объем для закрытия части позиции
                decimal volumeToClose = Math.Round(Math.Abs(position) * part, 8);

                // Округление объема до шага объема инструмента
                decimal volumeStep = Security.VolumeStep ?? 0.01m;
                volumeToClose = Math.Floor(volumeToClose / volumeStep) * volumeStep;

                // Проверка минимального объема
                if (volumeToClose < volumeStep)
                    volumeToClose = volumeStep;

                // Если объем слишком мал, закрываем всю позицию
                if (volumeToClose >= Math.Abs(position) - volumeStep)
                {
                    ClosePosition(positionSide, price, reason);
                    return;
                }

                // Закрытие части позиции
                if (positionSide == Sides.Buy)
                    SellMarket(volumeToClose);
                else
                    BuyMarket(volumeToClose);

                // Логирование частичного закрытия позиции
                LogInfo($"Закрыта часть позиции ({part * 100}%). Цена: {price}, Объем: {volumeToClose}, Причина: {reason}");
            }
            catch (Exception ex)
            {
                LogErrorDetailed("Ошибка при закрытии части позиции", ex);
            }
        }

        /// <summary>
        /// Закрытие позиции
        /// </summary>
        private void ClosePosition(Sides positionSide, decimal price, string reason)
        {
            try
            {
                var position = GetCurrentPosition();
                // Если позиции нет, выходим
                if (position == 0)
                    return;

                // Закрытие всей позиции
                if (positionSide == Sides.Buy)
                    SellMarket(Math.Abs(position));
                else
                    BuyMarket(Math.Abs(position));

                // Сброс состояния стратегии
                _isPositionOpened = false;
                _isTrailingStopActivated = false;

                // Логирование закрытия позиции
                LogInfo($"Позиция закрыта. Цена: {price}, Объем: {Math.Abs(position)}, Причина: {reason}");
            }
            catch (Exception ex)
            {
                LogErrorDetailed("Ошибка при закрытии позиции", ex);
            }
        }

        private decimal GetCurrentPosition()
        {
            
            if (Position == 0)
            {
                var p = Positions?.FirstOrDefault()?.CurrentValue;
                if (p != null)
                    return (decimal)p;

            }
            
                return Position;
        }
    }
}