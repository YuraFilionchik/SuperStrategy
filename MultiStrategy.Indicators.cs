namespace SuperStrategy
{
    using System;
    using GeneticSharp;
    using StockSharp.Algo.Candles;
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
        private bool isPortfolioInitialized = false;
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
               // UpdateChart(candle);

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
                if (Portfolio != null && !isPortfolioInitialized)
                    InitializePortfolio(candle.OpenPrice);
                
                // Если свеча не финальная, то выходим
                if (candle.State != CandleStates.Finished)
                    return;
                
                #region Обработка индикаторов и сохранение текущих значений

                // Сохраняем предыдущие значения
                _previousFastEma = _currentFastEma;
                _previousSlowEma = _currentSlowEma;
                _previousRsi = _currentRsi;
                _previousObv = _currentObv;

                ProcessIndicators(candle);

                

                
                #endregion
                // Логирование значений индикаторов
                //LogInfo($"Индикаторы: FastEMA={_currentFastEma:F8}, SlowEMA={_currentSlowEma:F8}, RSI={_currentRsi:F2}, ATR={_currentAtr:F8}");
                //LogInfo($"Bollinger: Mid={_currentMiddleBand:F8}, Up={_currentUpperBand:F8}, Low={_currentLowerBand:F8}");

                // Обновляем график
                UpdateChart(candle);

                // Если индикаторы не сформированы, выходим
                if (!IsFormedAndOnlineAndAllowTrading())
                    return;
                decimal tradeVolume = 1000m;
                var Position = GetCurrentPosition();
                var rsi = _currentRsi;
                //test
                if (Position == 0)
                {
                    if (rsi < 10)
                        BuyMarket(tradeVolume);
                    if (rsi > 90)
                        SellMarket(tradeVolume);
                    return;
                }
                else
                {
                    if ((rsi < 50 && Position < 0) ||
                        (rsi > 50 && Position > 0))
                        CloseCurrentPosition(Position);
                    return;
                }
                //end test


                // Проверка сигналов и управление позицией
                if (_isPositionOpened)
                {
                    //ManagePosition(candle);
                    if ((CurrentTime - _positionOpenTime).TotalMinutes > 60)
                    {
                        ClosePosition();
                        LogInfo($"ClosePosition - {CurrentTime}");
                        _isPositionOpened = false;
                        return;
                    }
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
        
        void ProcessIndicators(ICandleMessage candle)
        {
            _fastEma.Process(candle);
            _slowEma.Process(candle);
            _rsi.Process(candle);
            _atr.Process(candle);
            _obv.Process(candle);
            _bollingerBands.Process(candle);

            _currentFastEma = _fastEma.GetCurrentValue();
            _currentSlowEma = _slowEma.GetCurrentValue();
            _currentRsi = _rsi.GetCurrentValue();
            _currentAtr = _atr.GetCurrentValue();
            _currentObv = _obv.GetCurrentValue();

            // Обработка Bollinger Bands
            try
            {
                _currentMiddleBand = _bollingerBands.MovingAverage.GetCurrentValue();

                // Для верхней и нижней полосы используем несколько подходов
                if (_bollingerBands.UpBand != null && _bollingerBands.LowBand != null)
                {
                    _bollingerBands.UpBand.Process(candle);
                    _bollingerBands.LowBand.Process(candle);
                    _currentUpperBand = _bollingerBands.UpBand.GetCurrentValue();
                    _currentLowerBand = _bollingerBands.LowBand.GetCurrentValue();
                }
                else
                {
                    _currentUpperBand = 0;
                    _currentLowerBand = 0;
                }
            }
            catch (Exception ex)
            {
                LogError($"Ошибка при обработке Bollinger Bands: {ex.Message}");
            }
        }
    }
}