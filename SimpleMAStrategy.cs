using System;
using System.Collections.Generic;
using Ecng.Logging;
using StockSharp.Algo;
using StockSharp.Algo.Candles;
using StockSharp.Algo.Indicators;
using StockSharp.Algo.Strategies;
using StockSharp.BusinessEntities;
using StockSharp.Messages;

namespace SuperStrategy
{
    public class SimpleMAStrategy : Strategy
    {
        private readonly SimpleMovingAverage _shortSma;
        private readonly SimpleMovingAverage _longSma;
        private bool? _isShortLessThenLong;
        private readonly StrategyParam<int> _shortPeriod;
        private readonly StrategyParam<int> _longPeriod;
        private readonly StrategyParam<decimal> _volume;
        private readonly StrategyParam<DataType> _candleType;

        public SimpleMAStrategy()
        {
            // Инициализация параметров стратегии
            _shortPeriod = Param("ShortPeriod", 10);
            _longPeriod = Param("LongPeriod", 30);
            _volume = Param("TradeVolume", 1m);
            _candleType = Param("CandleType", DataType.TimeFrame(TimeSpan.FromMinutes(5)));

            // Создание индикаторов
            _shortSma = new SimpleMovingAverage { Length = ShortPeriod };
            _longSma = new SimpleMovingAverage { Length = LongPeriod };
            
        }

        // Параметры стратегии, доступные для настройки в Designer
        public int ShortPeriod
        {
            get => _shortPeriod.Value;
            set
            {
                _shortPeriod.Value = value;
                _shortSma.Length = value;
            }
        }

        public int LongPeriod
        {
            get => _longPeriod.Value;
            set
            {
                _longPeriod.Value = value;
                _longSma.Length = value;
            }
        }

        public decimal TradeVolume
        {
            get => _volume.Value;
            set => _volume.Value = value;
        }

        public DataType CandleType
        {
            get => _candleType.Value;
            set => _candleType.Value = value;
        }

        protected override void OnStarted(DateTimeOffset time)
        {
            base.OnStarted(time);

            // Добавляем индикаторы в коллекцию стратегии
            Indicators.Add(_shortSma);
            Indicators.Add(_longSma);

            // Создаем и подписываемся на свечи
            var subscription = new Subscription(CandleType, Security);

            this.WhenCandlesFinished(subscription)
                .Do(ProcessCandle)
                .Apply(this);

            Subscribe(subscription);
        }

        private void ProcessCandle(ICandleMessage candle)
        {
            // Обработка свечи индикаторами
            var shortValue = _shortSma.Process(candle);
            var longValue = _longSma.Process(candle);

            if (!_shortSma.IsFormed)
                return;
            this.AddInfoLog($"Свеча: Время={candle.OpenTime}, Цена={candle.ClosePrice}, Объем={candle.TotalVolume}");
            // Проверка пересечения
            var isShortLessThenLong = shortValue.GetValue<decimal>() < longValue.GetValue<decimal>();

            // При первом вызове просто запоминаем текущее положение линий
            if (_isShortLessThenLong == null)
            {
                _isShortLessThenLong = isShortLessThenLong;
                return;
            }

            // Если направление изменилось - торгуем
            if (_isShortLessThenLong != isShortLessThenLong)
            {
                // Определяем направление сделки
                var direction = isShortLessThenLong ? Sides.Sell : Sides.Buy;

                // Объем позиции равен указанному параметру + текущая позиция (для закрытия и переворота)
                var volume = Position == 0
                    ? TradeVolume
                    : TradeVolume + Math.Abs(Position);

                // Выставляем заявку
                if (direction == Sides.Buy)
                    BuyMarket(volume);
                else
                    SellMarket(volume);

                // Сохраняем новое состояние
                _isShortLessThenLong = isShortLessThenLong;
            }
        }

        // Информация для Designer о используемых данных
        public override IEnumerable<(Security sec, DataType dt)> GetWorkingSecurities()
        {
            return new[] { (Security, CandleType) };
        }
    }
}