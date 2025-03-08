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
            InitializeStatistics();
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
            LogPerformanceMetrics();
            
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

            // Если позиция открыта (или частично открыта)
            if (Math.Abs(Position) > 0 && _lastEntryPrice == 0)
            {
                _lastEntryPrice = trade.Trade.Price;
                _tradeEntryVolume = trade.Trade.Volume;
                LogInfo($"Зафиксирована цена входа: {_lastEntryPrice}, объем: {_tradeEntryVolume}");
            }
            // Если позиция закрыта (или частично закрыта)
            else if (Position == 0 || (Math.Sign(Position) != Math.Sign(Position - trade.Trade.Volume)))
            {
                if (_lastEntryPrice != 0)
                {
                    decimal volume = Math.Min(_tradeEntryVolume, trade.Trade.Volume);
                    decimal pnl;

                    // Расчет PnL
                    if (trade.Order.Side == Sides.Buy)
                    {
                        // Закрытие короткой позиции
                        pnl = (_lastEntryPrice - trade.Trade.Price) * volume;
                    }
                    else
                    {
                        // Закрытие длинной позиции
                        pnl = (trade.Trade.Price - _lastEntryPrice) * volume;
                    }

                    // Обновляем статистику
                    _totalPnL += pnl;

                    string resultText = pnl > 0 ? "ПРИБЫЛЬНАЯ" : "УБЫТОЧНАЯ";
                    LogInfo($"Сделка {resultText}. PnL: {pnl}. Всего PnL: {_totalPnL}");

                    if (pnl > 0)
                    {
                        _winCount++;
                        _winningPnL += pnl;
                    }
                    else
                    {
                        _lossCount++;
                        _losingPnL += Math.Abs(pnl);
                    }

                    // Обновляем статистику
                    decimal winRate = (_winCount + _lossCount) > 0 ? (decimal)_winCount / (_winCount + _lossCount) : 0;
                    LogInfo($"Статистика: Побед: {_winCount}, Поражений: {_lossCount}, Винрейт: {winRate:P2}");

                    // Сбрасываем
                    if (Position == 0)
                    {
                        _lastEntryPrice = 0;
                        _tradeEntryVolume = 0;
                    }
                    else
                    {
                        // Если осталась часть позиции, обновляем данные
                        _tradeEntryVolume = Math.Abs(Position);
                    }
                }
            }
        }

        private void InitializeStatistics()
        {
            // Регистрируем счетчики для отслеживания статистики
            _winCount = 0;
            _lossCount = 0;
            _totalPnL = 0;
            _winningPnL = 0;
            _losingPnL = 0;

            LogInfo("Статистика инициализирована успешно");
        }

        private void LogPerformanceMetrics()
        {
            try
            {
                // Расчет метрик производительности
                decimal winRate = _winCount + _lossCount > 0
                    ? (decimal)_winCount / (_winCount + _lossCount)
                    : 0;

                decimal profitFactor = _losingPnL != 0
                    ? _winningPnL / _losingPnL
                    : 0;

                decimal averageWin = _winCount > 0
                    ? _winningPnL / _winCount
                    : 0;

                decimal averageLoss = _lossCount > 0
                    ? _losingPnL / _lossCount
                    : 0;

                decimal rrRatio = averageLoss != 0
                    ? averageWin / averageLoss
                    : 0;

                // Лог метрик производительности
                LogInfo($"===== ИТОГОВАЯ СТАТИСТИКА =====");
                LogInfo($"Общий PnL: {_totalPnL.ToString("N2")} USDT");
                LogInfo($"Винрейт: {winRate.ToString("P2")}");
                LogInfo($"Профит-фактор: {profitFactor.ToString("N2")}");
                LogInfo($"Средняя прибыль: {averageWin.ToString("N2")} USDT");
                LogInfo($"Средний убыток: {averageLoss.ToString("N2")} USDT");
                LogInfo($"Risk-Reward Ratio: {rrRatio.ToString("N2")}");
                LogInfo($"Всего сделок: {_winCount + _lossCount}");
                LogInfo($"Прибыльных сделок: {_winCount}");
                LogInfo($"Убыточных сделок: {_lossCount}");
                LogInfo($"=============================");
            }
            catch (Exception ex)
            {
                LogError($"Ошибка при логировании метрик: {ex.Message}");
            }
        }

        private void LogLevels(decimal entryPrice, decimal stopLossPrice, decimal takeProfitPrice)
        {
            LogInfo($"=== УРОВНИ ПОЗИЦИИ ===");
            LogInfo($"Цена входа: {entryPrice:F8}");
            LogInfo($"Stop-Loss: {stopLossPrice:F8} ({(Math.Abs(stopLossPrice - entryPrice) / entryPrice):P2})");
            LogInfo($"Take-Profit: {takeProfitPrice:F8} ({(Math.Abs(takeProfitPrice - entryPrice) / entryPrice):P2})");
            LogInfo($"=====================");
        }
    }
}