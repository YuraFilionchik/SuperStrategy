namespace SuperStrategy
{
    using System;
    using Ecng.Logging;
    using GeneticSharp;
    using StockSharp.Algo.Candles;
    using StockSharp.Algo.Indicators;
    using StockSharp.Charting;
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
        IIndicatorValue fastEmaValue;
        IIndicatorValue slowEmaValue;
        IIndicatorValue rsiValue;
        IIndicatorValue atrValue;
        IIndicatorValue obvValue;
        IIndicatorValue bbValue;
        IIndicatorValue longEmaValue;
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
                if (candle.State != CandleStates.Finished)
                    return;
                // Обработка 1-часовой свечи
                longEmaValue =_longEma.Process(candle);
                _currentLongEma = longEmaValue.GetValue<Decimal>();
                // Обновляем график
                if (_chart == null)
                    return;

                var data = _chart.CreateData();
                var group = data.Group(candle.OpenTime);
                group.Add(_longEmaElement, longEmaValue);
                // Рисуем данные
                _chart.Draw(data);
                //LogInfo($"Обработана 1-часовая свеча: {candle.CloseTime}, LongEMA={_currentLongEma}");
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
                
                if (Portfolio != null && !isPortfolioInitialized)
                    InitializePortfolio(candle.OpenPrice);
                
                #region Обработка индикаторов и сохранение текущих значений

                // Сохраняем предыдущие значения
                _previousFastEma = _currentFastEma;
                _previousSlowEma = _currentSlowEma;
                _previousRsi = _currentRsi;
                _previousObv = _currentObv;

                ProcessIndicators5m(candle);               
                                
                #endregion

                // Если индикаторы не сформированы, выходим
                if (!IsFormedAndOnlineAndAllowTrading())
                    return;

                decimal tradeVolume = (0.9m * TradeVolume) / candle.OpenPrice;
                var Position = GetCurrentPosition();
                //test
                var rsi = _currentRsi;
                if (Position == 0)
                {
                    if (rsi < 30)
                        BuyMarket(tradeVolume);
                    if (rsi > 67)
                        SellMarket(tradeVolume);
                    return;
                }
                else
                {
                    if ((rsi < 60 && Position < 0) ||
                        (rsi > 40 && Position > 0))
                        CloseCurrentPosition(Position);
                    return;
                }
                //end test

                // Проверка сигналов и управление позицией
                if (Position != 0)
                {
                    ManagePosition(candle);
                    if ((CurrentTime - _positionOpenTime).TotalMinutes > 60)
                    {
                        CloseCurrentPosition(Position);
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
                LogErrorDetailed($"Ошибка в ProcessCandles5m({candle.OpenTime} - {candle.CloseTime})", ex);
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
                   //_bollingerBands.IsFormed &&
                   _atr.IsFormed &&
                   _obv.IsFormed;
        }
        
        void ProcessIndicators5m(ICandleMessage candle)
        {
             fastEmaValue = _fastEma.Process(candle);
             slowEmaValue = _slowEma.Process(candle);
             rsiValue = _rsi.Process(candle);
             atrValue = _atr.Process(candle);
             obvValue = _obv.Process(candle);
             bbValue = _bollingerBands.Process(candle);
            
            // Преобразуем в десятичные значения
            if (fastEmaValue.IsFormed && !fastEmaValue.IsEmpty)
            _currentFastEma = fastEmaValue.GetValue<decimal>();
            
            if (slowEmaValue.IsFormed && !slowEmaValue.IsEmpty)
            _currentSlowEma = slowEmaValue.GetValue<decimal>();
            
            if (rsiValue.IsFormed && !rsiValue.IsEmpty)
            _currentRsi = rsiValue.GetValue<decimal>();
            
            if (atrValue.IsFormed && !atrValue.IsEmpty)
            _currentAtr = atrValue.GetValue<decimal>();
            
            if (obvValue.IsFormed && !obvValue.IsEmpty)
            _currentObv = obvValue.GetValue<decimal>();

            // Обработка Bollinger Bands
            //try
            //{  if (_bollingerBands.IsFormed)
            //    {
            //        _currentMiddleBand = _bollingerBands.GetCurrentValue();


            //        // Для верхней и нижней полосы используем несколько подходов
            //        if (_bollingerBands.UpBand != null && _bollingerBands.LowBand != null)
            //        {
            //            //_bollingerBands.UpBand.Process(candle);
            //            //_bollingerBands.LowBand.Process(candle);
            //            _currentUpperBand = _bollingerBands.UpBand.GetCurrentValue();
            //            _currentLowerBand = _bollingerBands.LowBand.GetCurrentValue();
            //        }
            //        else
            //        {
            //            _currentUpperBand = 0;
            //            _currentLowerBand = 0;
            //        }
            //    }else
            //    {
            //        _currentUpperBand = 0;
            //        _currentLowerBand = 0;
            //    }
            //}
            //catch (Exception ex)
            //{
            //    LogError($"Ошибка при обработке Bollinger Bands: {ex.Message}");
            //}

            //UpdateChart(candle, fastEmaValue, slowEmaValue, rsiValue);//, bbValue);
            UpdateChart(candle);
        }

        bool IsCandle1h(ICandleMessage candle)
        {
            if (candle.OpenTime != default && candle.CloseTime != default)
            {
                TimeSpan approximateTimeFrame = candle.CloseTime - candle.OpenTime;
                if (approximateTimeFrame > new TimeSpan(0, 59, 0) && approximateTimeFrame < new TimeSpan(1, 1, 0))
                    return true;
            }
            return false;
        }
    }
}