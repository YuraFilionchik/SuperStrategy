namespace SuperStrategy
{
    using System;
    using StockSharp.Messages;

    public partial class MultiStrategy
    {
        /// <summary>
        /// Проверка сигналов для входа в позицию
        /// </summary>
        private void CheckEntrySignals(ICandleMessage candle)
        {
            try
            {
                // Проверка, что все индикаторы сформированы
                if (!AreIndicatorsFormed())
                    return;

                // Получение текущей цены
                decimal lastPrice = candle.ClosePrice;

                //LogInfo($"Проверка сигналов: Цена={lastPrice}, FastEMA={_currentFastEma:F8}, SlowEMA={_currentSlowEma:F8}");

                // Проверка фильтров
                if (!CheckTimeFilter() || !CheckVolatilityFilter())
                {
                    //LogInfo("Фильтры не пройдены, сигнал отклонен");
                    return;
                }

                // Проверка сигналов для Long позиции
                bool isLongSignal = _currentFastEma > _currentSlowEma && // Быстрая EMA выше медленной
                                   _previousFastEma <= _previousSlowEma && // Пересечение снизу вверх
                                   lastPrice > _currentLongEma && // Цена выше длинной EMA
                                   _currentRsi > 40 && _previousRsi < 30 && // RSI поднимается выше 40 с уровней ниже 30
                                   lastPrice <= _currentLowerBand * 1.02m && // Цена около нижней полосы Боллинджера
                                   _currentObv > _previousObv; // Объем растет

                // Проверка сигналов для Short позиции
                bool isShortSignal = _currentFastEma < _currentSlowEma && // Быстрая EMA ниже медленной
                                    _previousFastEma >= _previousSlowEma && // Пересечение сверху вниз
                                    lastPrice < _currentLongEma && // Цена ниже длинной EMA
                                    _currentRsi < 60 && _previousRsi > 70 && // RSI опускается ниже 60 с уровней выше 70
                                    lastPrice >= _currentUpperBand * 0.98m && // Цена около верхней полосы Боллинджера
                                    _currentObv < _previousObv; // Объем падает

                // Логирование сигналов
                if (isLongSignal)
                    LogInfo("Обнаружен LONG сигнал");
                if (isShortSignal)
                    LogInfo("Обнаружен SHORT сигнал");

                // Открытие позиции
                if (isLongSignal && CheckGlobalTrendFilter(true))
                {
                    OpenPosition(Sides.Buy, lastPrice);
                }
                else if (isShortSignal && CheckGlobalTrendFilter(false))
                {
                    OpenPosition(Sides.Sell, lastPrice);
                }
            }
            catch (Exception ex)
            {
                LogErrorDetailed("Ошибка при проверке сигналов входа", ex);
            }
        }

        /// <summary>
        /// Проверка сигналов разворота
        /// </summary>
        private bool CheckReverseSignals(ICandleMessage candle, Sides positionSide)
        {
            try
            {
                // Проверка пересечения EMA в противоположном направлении
                bool isFastCrossedBelow = _currentFastEma < _currentSlowEma &&
                                        _previousFastEma >= _previousSlowEma;

                bool isFastCrossedAbove = _currentFastEma > _currentSlowEma &&
                                        _previousFastEma <= _previousSlowEma;

                if ((positionSide == Sides.Buy && isFastCrossedBelow) ||
                    (positionSide == Sides.Sell && isFastCrossedAbove))
                {
                    LogInfo($"Обнаружен сигнал разворота: пересечение EMA в противоположном направлении");
                    return true;
                }

                // Проверка экстремальных значений RSI
                if ((positionSide == Sides.Buy && _currentRsi > 80) ||
                    (positionSide == Sides.Sell && _currentRsi < 20))
                {
                    LogInfo($"Обнаружен сигнал разворота: экстремальное значение RSI = {_currentRsi}");
                    return true;
                }

                // Проверка свечных моделей разворота
                bool isDoji = Math.Abs(candle.OpenPrice - candle.ClosePrice) / candle.OpenPrice < 0.001m &&
                            (candle.HighPrice - candle.LowPrice) / candle.OpenPrice > 0.005m;

                if (isDoji)
                {
                    LogInfo("Обнаружен сигнал разворота: паттерн доджи");
                    return true;
                }

                return false;
            }
            catch (Exception ex)
            {
                LogErrorDetailed("Ошибка при проверке сигналов разворота", ex);
                return false;
            }
        }

        /// <summary>
        /// Фильтр времени
        /// </summary>
        private bool CheckTimeFilter()
        {
            // Проверка времени сессии - можно настроить для конкретной биржи
            return true;
        }

        /// <summary>
        /// Фильтр волатильности
        /// </summary>
        private bool CheckVolatilityFilter()
        {
            try
            {
                // Проверка минимальной волатильности
                decimal minRequiredAtr = Security.PriceStep.GetValueOrDefault(0.0001m) * MinVolatilityMultiplier;

                if (_currentAtr < minRequiredAtr)
                {
                    LogInfo($"Низкая волатильность: ATR={_currentAtr:F8}, требуется минимум {minRequiredAtr:F8}");
                    return false;
                }

                return true;
            }
            catch (Exception ex)
            {
                LogError($"Ошибка в фильтре волатильности: {ex.Message}");
                return true; // По умолчанию пропускаем сигнал при ошибке
            }
        }

        /// <summary>
        /// Фильтр глобального тренда
        /// </summary>
        private bool CheckGlobalTrendFilter(bool isLong)
        {
            try
            {
                // Определение направления глобального тренда по EMA 55
                bool isUptrend = _currentLongEma > _previousLongEma;

                bool result = isLong == isUptrend;

                if (!result)
                {
                    LogInfo($"Фильтр глобального тренда не пройден: isLong={isLong}, isUptrend={isUptrend}");
                }

                // Торговать только по направлению глобального тренда
                return result;
            }
            catch (Exception ex)
            {
                LogError($"Ошибка в фильтре глобального тренда: {ex.Message}");
                return true; // По умолчанию пропускаем сигнал при ошибке
            }
        }
    }
}