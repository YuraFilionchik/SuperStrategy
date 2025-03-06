namespace SuperStrategy
{
    using System;
    using Ecng.Logging;
    using StockSharp.Algo;
    using StockSharp.Algo.Strategies;
    using StockSharp.BusinessEntities;
    using StockSharp.Messages;

    /// <summary>
    /// Основной класс стратегии для торговли на крипторынке
    /// </summary>
    public partial class MultiStrategy : Strategy
    {
        /// <summary>
        /// Конструктор стратегии
        /// </summary>
        public MultiStrategy()
        {
            // Инициализация логирования
            InitializeLogging();

            // Инициализация параметров
            InitializeParameters();

            // Инициализация индикаторов
            InitializeIndicators();
            
            LogLevel = LogLevels.Info;
        }

        

        /// <summary>
        /// Запуск стратегии
        /// </summary>
        protected override void OnStarted(DateTimeOffset time)
        {
            base.OnStarted(time);
            this.NewMyTrade += OnNewMyTrade;
            // Логирование запуска стратегии
            LogInfo("Стратегия запущена.");

            // Добавляем индикаторы в коллекцию стратегии
            AddIndicatorsToStrategy();
            // Создаем и подписываемся на свечи
            var subscription5m = new Subscription(TimeFrame5m, Security);
            var subscription1h = new Subscription(TimeFrame1h, Security);

            // Инициализируем график
            InitializeChart();

            // Устанавливаем обработчики для свечей
            this.WhenCandlesFinished(subscription5m)
                .Do(ProcessCandles5m)
                .Apply(this);

            this.WhenCandlesFinished(subscription1h)
                .Do(ProcessCandles1h)
                .Apply(this);

            // Подписываемся на свечи
            Subscribe(subscription5m);
            Subscribe(subscription1h);
            if (Portfolio!=null)
            InitializePortfolio();
        }

        protected override void OnStopped()
        {
            // Логирование остановки стратегии
            LogInfo("Стратегия остановлена.");

            base.OnStopped();
        }

        /// <summary>
        /// Информация о используемых данных для Designer
        /// </summary>
        public override IEnumerable<(Security sec, DataType dt)> GetWorkingSecurities()
        {
            return new[]
            {
                (Security, TimeFrame5m),
                (Security, TimeFrame1h)
            };
        }

        protected override void OnNewMyTrade(MyTrade trade)
        {
            LogInfo($"Новая сделка: {trade.Trade.Id}, Цена: {trade.Trade.Price}, Объем: {trade.Trade.Volume}, Position: {Position}");
            _isPositionOpened = Position != 0;
        }

    }
}