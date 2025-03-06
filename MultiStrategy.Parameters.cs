namespace SuperStrategy
{
    using System;
    using StockSharp.Algo.Indicators;
    using StockSharp.Algo.Strategies;
    using StockSharp.Messages;

    public partial class MultiStrategy
    {
        // Параметры стратегии
        private StrategyParam<int> _fastEmaPeriod;
        private StrategyParam<int> _slowEmaPeriod;
        private StrategyParam<int> _longEmaPeriod;
        private StrategyParam<int> _rsiPeriod;
        private StrategyParam<int> _bbPeriod;
        private StrategyParam<int> _atrPeriod;
        private StrategyParam<decimal> _tradeVolume;
        private StrategyParam<decimal> _stopLossMultiplier;
        private StrategyParam<decimal> _takeProfitMultiplier1;
        private StrategyParam<decimal> _takeProfitMultiplier2;
        private StrategyParam<decimal> _takeProfitMultiplier3;
        private StrategyParam<decimal> _trailingStopMultiplier;
        private StrategyParam<decimal> _trailingStopStep;
        private StrategyParam<decimal> _riskPerTrade;
        private StrategyParam<DataType> _timeFrame5m;
        private StrategyParam<DataType> _timeFrame1h;
        private StrategyParam<decimal> _minVolatilityMultiplier;

        /// <summary>
        /// Инициализация параметров стратегии
        /// </summary>
        private void InitializeParameters()
        {
            // Инициализация параметров стратегии
            _fastEmaPeriod = Param("FastEmaPeriod", 8)
                .SetDisplay("Период быстрой EMA", "Период для быстрой EMA (по умолчанию 8)", "Индикаторы тренда");

            _slowEmaPeriod = Param("SlowEmaPeriod", 21)
                .SetDisplay("Период медленной EMA", "Период для медленной EMA (по умолчанию 21)", "Индикаторы тренда");

            _longEmaPeriod = Param("LongEmaPeriod", 55)
                .SetDisplay("Период длинной EMA", "Период для длинной EMA (по умолчанию 55)", "Индикаторы тренда");

            _rsiPeriod = Param("RsiPeriod", 14)
                .SetDisplay("Период RSI", "Период для RSI (по умолчанию 14)", "Индикаторы перекупленности/перепроданности");

            _bbPeriod = Param("BollingerBandsPeriod", 20)
                .SetDisplay("Период Bollinger Bands", "Период для Bollinger Bands (по умолчанию 20)", "Индикаторы волатильности");

            _atrPeriod = Param("AtrPeriod", 14)
                .SetDisplay("Период ATR", "Период для ATR (по умолчанию 14)", "Индикаторы волатильности");

            _tradeVolume = Param("TradeVolume", 1.0m)
                .SetDisplay("Объем торговли, USDT", "Базовый объем для торговли (по умолчанию 1.0)", "Управление позицией");

            _stopLossMultiplier = Param("StopLossMultiplier", 1.5m)
                .SetDisplay("Множитель Stop-Loss", "Множитель ATR для Stop-Loss (по умолчанию 1.5)", "Управление позицией");

            _takeProfitMultiplier1 = Param("TakeProfitMultiplier1", 2.0m)
                .SetDisplay("Множитель TP1", "Множитель ATR для первого Take-Profit (по умолчанию 2.0)", "Управление позицией");

            _takeProfitMultiplier2 = Param("TakeProfitMultiplier2", 3.0m)
                .SetDisplay("Множитель TP2", "Множитель ATR для второго Take-Profit (по умолчанию 3.0)", "Управление позицией");

            _takeProfitMultiplier3 = Param("TakeProfitMultiplier3", 5.0m)
                .SetDisplay("Множитель TP3", "Множитель ATR для третьего Take-Profit (по умолчанию 5.0)", "Управление позицией");

            _trailingStopMultiplier = Param("TrailingStopMultiplier", 2.0m)
                .SetDisplay("Множитель Trailing Stop", "Множитель ATR для Trailing Stop (по умолчанию 2.0)", "Управление позицией");

            _trailingStopStep = Param("TrailingStopStep", 0.5m)
                .SetDisplay("Шаг Trailing Stop", "Шаг ATR для Trailing Stop (по умолчанию 0.5)", "Управление позицией");

            _riskPerTrade = Param("RiskPerTrade", 0.01m)
                .SetDisplay("Риск на сделку", "Процент риска на одну сделку (по умолчанию 0.01 = 1%)", "Управление рисками");

            _timeFrame5m = Param("TimeFrame5m", DataType.TimeFrame(TimeSpan.FromMinutes(5)))
                .SetDisplay("Таймфрейм 5м", "Таймфрейм для основной торговли (5 минут)", "Таймфреймы");

            _timeFrame1h = Param("TimeFrame1h", DataType.TimeFrame(TimeSpan.FromHours(1)))
                .SetDisplay("Таймфрейм 1ч", "Таймфрейм для определения тренда (1 час)", "Таймфреймы");
            
            _minVolatilityMultiplier = Param("MinVolatilityMultiplier", 10.0m)
                .SetDisplay("Множитель минимальной волатильности",
               "Множитель ценового шага для определения минимальной приемлемой волатильности (по умолчанию 10.0)",
               "Фильтры");
        }

        #region Properties
        // Свойства для параметров стратегии
        public int FastEmaPeriod
        {
            get => _fastEmaPeriod.Value;
            set
            {
                _fastEmaPeriod.Value = value;
                if (_fastEma != null)
                    _fastEma.Length = value;
            }
        }

        public int SlowEmaPeriod
        {
            get => _slowEmaPeriod.Value;
            set
            {
                _slowEmaPeriod.Value = value;
                if (_slowEma != null)
                    _slowEma.Length = value;
            }
        }

        public int LongEmaPeriod
        {
            get => _longEmaPeriod.Value;
            set
            {
                _longEmaPeriod.Value = value;
                if (_longEma != null)
                    _longEma.Length = value;
            }
        }

        public int RsiPeriod
        {
            get => _rsiPeriod.Value;
            set
            {
                _rsiPeriod.Value = value;
                if (_rsi != null)
                    _rsi.Length = value;
            }
        }

        public int BollingerBandsPeriod
        {
            get => _bbPeriod.Value;
            set
            {
                _bbPeriod.Value = value;
                if (_bollingerBands != null)
                    _bollingerBands.Length = value;
            }
        }

        public int AtrPeriod
        {
            get => _atrPeriod.Value;
            set
            {
                _atrPeriod.Value = value;
                if (_atr != null)
                    _atr.Length = value;
            }
        }

        public decimal TradeVolume
        {
            get => _tradeVolume.Value;
            set => _tradeVolume.Value = value;
        }

        public decimal StopLossMultiplier
        {
            get => _stopLossMultiplier.Value;
            set => _stopLossMultiplier.Value = value;
        }

        public decimal TakeProfitMultiplier1
        {
            get => _takeProfitMultiplier1.Value;
            set => _takeProfitMultiplier1.Value = value;
        }

        public decimal TakeProfitMultiplier2
        {
            get => _takeProfitMultiplier2.Value;
            set => _takeProfitMultiplier2.Value = value;
        }

        public decimal TakeProfitMultiplier3
        {
            get => _takeProfitMultiplier3.Value;
            set => _takeProfitMultiplier3.Value = value;
        }

        public decimal TrailingStopMultiplier
        {
            get => _trailingStopMultiplier.Value;
            set => _trailingStopMultiplier.Value = value;
        }

        public decimal TrailingStopStep
        {
            get => _trailingStopStep.Value;
            set => _trailingStopStep.Value = value;
        }

        public decimal RiskPerTrade
        {
            get => _riskPerTrade.Value;
            set => _riskPerTrade.Value = value;
        }

        public DataType TimeFrame5m
        {
            get => _timeFrame5m.Value;
            set => _timeFrame5m.Value = value;
        }

        public DataType TimeFrame1h
        {
            get => _timeFrame1h.Value;
            set => _timeFrame1h.Value = value;
        }

        public decimal MinVolatilityMultiplier
        {
            get => _minVolatilityMultiplier.Value;
            set => _minVolatilityMultiplier.Value = value;
        }
        #endregion

        private void InitializePortfolio()
        {
            Portfolio.BeginValue = 100;
            Portfolio.CurrentValue = 100;
            Portfolio.Currency = Ecng.Common.CurrencyTypes.USDT;
            Portfolio.Name = "Test";

        }
    }
}