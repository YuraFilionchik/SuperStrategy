namespace SuperStrategy
{
    using System;
    using StockSharp.Algo.Indicators;
    using StockSharp.Messages;

    public partial class MultiStrategy
    {
        // Индикаторы тренда
        private ExponentialMovingAverage _fastEma;
        private ExponentialMovingAverage _slowEma;
        private ExponentialMovingAverage _longEma;

        // Индикаторы перекупленности/перепроданности
        private RelativeStrengthIndex _rsi;

        // Индикаторы волатильности
        private BollingerBands _bollingerBands;
        private AverageTrueRange _atr;

        // Объемные индикаторы
        private OnBalanceVolume _obv;

        // Текущие и предыдущие значения индикаторов
        private decimal _currentFastEma;
        private decimal _previousFastEma;
        private decimal _currentSlowEma;
        private decimal _previousSlowEma;
        private decimal _currentLongEma;
        private decimal _previousLongEma;
        private decimal _currentRsi;
        private decimal _previousRsi;
        private decimal _currentAtr;
        private decimal _currentUpperBand;
        private decimal _currentMiddleBand;
        private decimal _currentLowerBand;
        private decimal _currentObv;
        private decimal _previousObv;

        /// <summary>
        /// Инициализация индикаторов
        /// </summary>
        private void InitializeIndicators()
        {
            try
            {
                // Инициализация индикаторов
                _fastEma = new ExponentialMovingAverage { Length = FastEmaPeriod };
                _slowEma = new ExponentialMovingAverage { Length = SlowEmaPeriod };
                _longEma = new ExponentialMovingAverage { Length = LongEmaPeriod };
                _rsi = new RelativeStrengthIndex { Length = RsiPeriod };

                _bollingerBands = new BollingerBands
                {
                    Length = BollingerBandsPeriod,
                    Width = 2.0m  // Стандартное отклонение
                };

                _atr = new AverageTrueRange { Length = AtrPeriod };
                _obv = new OnBalanceVolume();

                LogInfo("Индикаторы инициализированы успешно");
            }
            catch (Exception ex)
            {
                LogErrorDetailed("Ошибка при инициализации индикаторов", ex);
            }
        }

        /// <summary>
        /// Добавление индикаторов в коллекцию стратегии
        /// </summary>
        private void AddIndicatorsToStrategy()
        {
            // Добавляем индикаторы в коллекцию стратегии
            Indicators.Add(_fastEma);
            Indicators.Add(_slowEma);
            Indicators.Add(_longEma);
            Indicators.Add(_rsi);
            Indicators.Add(_bollingerBands);
            Indicators.Add(_atr);
            Indicators.Add(_obv);

            LogInfo("Индикаторы добавлены в коллекцию стратегии");
        }

        /// <summary>
        /// Обработка 1-часовой свечи
        /// </summary>
        private void ProcessCandles1h(ICandleMessage candle)
        {
            try
            {
                // Обработка 1-часовой свечи
                _longEma.Process(candle);
                _currentLongEma = _longEma.GetCurrentValue();

                // Обновляем график
                UpdateChart(candle);

               // LogInfo($"Обработана 1-часовая свеча: {candle.OpenTime}, LongEMA={_currentLongEma}");
            }
            catch (Exception ex)
            {
                LogErrorDetailed("Ошибка в ProcessCandles1h", ex);
            }
        }

        /// <summary>
        /// Обработка 5-минутной свечи
        /// </summary>
        private void ProcessCandles5m(ICandleMessage candle)
        {
            try
            {
                // Если свеча не финальная, то выходим
                if (candle.State != CandleStates.Finished)
                    return;

                // Сохраняем предыдущие значения
                _previousFastEma = _currentFastEma;
                _previousSlowEma = _currentSlowEma;
                _previousRsi = _currentRsi;
                _previousObv = _currentObv;

                // Обработка индикаторов и сохранение текущих значений
                _fastEma.Process(candle);
                _currentFastEma = _fastEma.GetCurrentValue();

                _slowEma.Process(candle);
                _currentSlowEma = _slowEma.GetCurrentValue();

                _rsi.Process(candle);
                _currentRsi = _rsi.GetCurrentValue();

                _atr.Process(candle);
                _currentAtr = _atr.GetCurrentValue();

                _obv.Process(candle);
                _currentObv = _obv.GetCurrentValue();

                // Обработка Bollinger Bands
                try
                {
                    _bollingerBands.Process(candle);
                    _currentMiddleBand = _bollingerBands.MovingAverage.GetCurrentValue();

                    // Для верхней и нижней полосы используем несколько подходов
                    if (_bollingerBands.UpBand != null && _bollingerBands.LowBand != null)
                    {
                        _currentUpperBand = _bollingerBands.UpBand.GetCurrentValue();
                        _currentLowerBand = _bollingerBands.LowBand.GetCurrentValue();
                    }
                    else
                    {
                        // Расчет на основе SMA и стандартного отклонения
                        decimal stdDev = 2.0m;
                        decimal volatility = _currentAtr;
                        _currentUpperBand = _currentMiddleBand + (volatility * stdDev);
                        _currentLowerBand = _currentMiddleBand - (volatility * stdDev);
                    }
                }
                catch (Exception ex)
                {
                    LogError($"Ошибка при обработке Bollinger Bands: {ex.Message}");
                    // Используем SMA вместо Bollinger в случае ошибки
                    _currentMiddleBand = _currentSlowEma;
                    _currentUpperBand = _currentMiddleBand * 1.02m;
                    _currentLowerBand = _currentMiddleBand * 0.98m;
                }

                // Логирование значений индикаторов
                //LogInfo($"Индикаторы: FastEMA={_currentFastEma:F8}, SlowEMA={_currentSlowEma:F8}, RSI={_currentRsi:F2}, ATR={_currentAtr:F8}");
                //LogInfo($"Bollinger: Mid={_currentMiddleBand:F8}, Up={_currentUpperBand:F8}, Low={_currentLowerBand:F8}");

                // Обновляем график
                UpdateChart(candle);

                // Если индикаторы не сформированы, выходим
                if (!IsFormedAndOnlineAndAllowTrading())
                    return;

                // Проверка сигналов и управление позицией
                if (_isPositionOpened)
                {
                    LogInfo($"Position is OPENED, POSITION = {Position}");
                    ManagePosition(candle);
                }
                else
                {
                    CheckEntrySignals(candle);
                }
            }
            catch (Exception ex)
            {
                LogErrorDetailed("Ошибка в ProcessCandles5m", ex);
            }
        }

        /// <summary>
        /// Проверка формирования всех индикаторов
        /// </summary>
        private bool AreIndicatorsFormed()
        {
            return _fastEma.IsFormed &&
                   _slowEma.IsFormed &&
                   _longEma.IsFormed &&
                   _rsi.IsFormed &&
                   _bollingerBands.IsFormed &&
                   _atr.IsFormed &&
                   _obv.IsFormed;
        }
    }
}