namespace SuperStrategy
{
    using System;
    using Ecng.Logging;
    using NuGet.Common;
    using StockSharp.Algo;
    using StockSharp.Algo.Strategies;
    using StockSharp.BusinessEntities;
    using StockSharp.Logging;
    using StockSharp.Messages;

    /// <summary>
    /// Основной класс стратегии для торговли на крипторынке
    /// </summary>
    public partial class MultiStrategy : Strategy
    {
         ///<summary>
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

            if (!IsSecurityValid())
            {
                Stop(new("Плохие данные Security"));
            }

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
            base.OnNewMyTrade(trade);
            var Position = GetCurrentPosition();
            _isPositionOpened = Position != 0;
            
            // Если позиция открыта (или частично открыта)
            if (Position != 0 && _lastEntryPrice == 0)
            {
                _lastEntryPrice = trade.Trade.Price;
                _tradeEntryVolume = trade.Trade.Volume;
                LogInfo($"Зафиксирована цена входа: {_lastEntryPrice}, объем: {_tradeEntryVolume} ({_lastEntryPrice * _tradeEntryVolume}$)");
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

        protected override void OnOrderRegisterFailed(OrderFail fail, bool calcRisk)
        {
            base.OnOrderRegisterFailed(fail, calcRisk);
            LogError($"Order registration failed: {fail.Error}");
        }

        protected override void OnOrderChanged(Order order)
        {
            base.OnOrderChanged(order);
            var Position = GetCurrentPosition();
            if (order.State == OrderStates.Done)
            {
                LogInfo($"Order {order.TransactionId} executed. Position now: {Position}");
            }
            else if (order.State == OrderStates.Failed)
            {
                LogError($"Order {order.TransactionId} failed");
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
        
        private bool IsSecurityValid()
        {
            if (Security == null)
            {
                LogError("Security is null. Стратегия не была запущена");
                return false;
            }
            return true;
            //LogInfo("======Security info======");
            //LogInfo(Security.ToString());
            //LogInfo($"Security settings: VolumeStep={Security.VolumeStep}, PriceStep={Security.PriceStep}");

            //bool result = Security.VolumeStep != null && Security.VolumeStep != 0 &&
            //    Security.PriceStep != null && Security.PriceStep != 0;

            //if (!result) LogInfo("Стратегия не запущена из-за плохих данных Security");
            //return result;
                
        }

        private void InitializePortfolio(Decimal price)
        {
            //Portfolio.BeginValue = TradeVolume     / price;
            //Portfolio.CurrentValue = TradeVolume / price;
            //Portfolio.Currency = Ecng.Common.CurrencyTypes.USDT;
            //Portfolio.Security = Security;
            //Portfolio.StrategyId = this.Id.ToString();
            
            isPortfolioInitialized = true;
        }
    }
}