namespace SuperStrategy
{
    using System;
    using StockSharp.Messages;
    using StockSharp.Charting;
    using StockSharp.Algo.Indicators;

    public partial class MultiStrategy
    {
        // Элементы графика
        private IChartTradeElement _tradesElement;
        private IChartCandleElement _candleElement;
        private IChartIndicatorElement _fastEmaElement;
        private IChartIndicatorElement _slowEmaElement;
        private IChartIndicatorElement _longEmaElement;
        private IChartIndicatorElement _rsiElement;
        private IChartIndicatorElement _bollingerUpperElement;
        private IChartIndicatorElement _bollingerMiddleElement;
        private IChartIndicatorElement _bollingerLowerElement;
        private IChartOrderElement _ordersElement;
        private IChartLineElement _stopLossLine;
        private IChartLineElement _takeProfitLine;

        /// <summary>
        /// Инициализация графика
        /// </summary>
        private void InitializeChart()
        {
            try
            {
                var chart = GetChart();
                if (chart == null)
                {
                    LogInfo("Chart unavailable, visualization disabled.");
                    return;
                }

                // Create chart areas
                var priceArea = CreateChartArea();
                var indicatorArea = CreateChartArea();

                // Add elements to chart
                _candleElement = priceArea.AddCandles();
                _tradesElement = priceArea.AddTrades();

                // Add order and trade visualization
                _ordersElement = DrawOrders(priceArea);
                _tradesElement = DrawOwnTrades(priceArea);

                // Add indicators
                _fastEmaElement = priceArea.AddIndicator(_fastEma);
                _fastEmaElement.Color = System.Drawing.Color.Blue;

                _slowEmaElement = priceArea.AddIndicator(_slowEma);
                _slowEmaElement.Color = System.Drawing.Color.Red;

                _longEmaElement = priceArea.AddIndicator(_longEma);
                _longEmaElement.Color = System.Drawing.Color.Purple;

                _bollingerUpperElement = priceArea.AddIndicator(_bollingerBands.UpBand);
                _bollingerLowerElement = priceArea.AddIndicator(_bollingerBands.LowBand);

                _rsiElement = indicatorArea.AddIndicator(_rsi);

                LogInfo("Chart initialized successfully.");
            }
            catch (Exception ex)
            {
                LogErrorDetailed("Error initializing chart", ex);
            }
        }
        //private void InitializeChart()
        //{
        //    try
        //    {
        //        var chart = GetChart();
        //        if (chart == null)
        //        {
        //            LogInfo("График недоступен, визуализация отключена.");
        //            return;
        //        }

        //        // Создаем области графика
        //        var priceArea = CreateChartArea();
        //        var indicatorArea = CreateChartArea();

        //        // Добавляем элементы на график
        //        _candleElement = priceArea.AddCandles();
        //        _tradesElement = priceArea.AddTrades();

        //        // Добавляем индикаторы на график
        //        _fastEmaElement = priceArea.AddIndicator(_fastEma);
        //        _slowEmaElement = priceArea.AddIndicator(_slowEma);
        //        _longEmaElement = priceArea.AddIndicator(_longEma);

        //        _bollingerUpperElement = priceArea.AddIndicator(_bollingerBands);
        //        _bollingerMiddleElement = priceArea.AddIndicator(_bollingerBands);
        //        _bollingerLowerElement = priceArea.AddIndicator(_bollingerBands);

        //        _rsiElement = indicatorArea.AddIndicator(_rsi);

        //        // Настраиваем отображение собственных сделок
        //        DrawOwnTrades(priceArea);
        //        DrawOrders(priceArea);

        //        LogInfo("График инициализирован успешно.");
        //    }
        //    catch (Exception ex)
        //    {
        //        LogErrorDetailed("Ошибка при инициализации графика", ex);
        //    }
        //}

        // Update chart with stop-loss and take-profit levels
        //private void UpdateStopLossAndTakeProfitLines(decimal stopLossPrice, decimal takeProfitPrice, DateTimeOffset time)
        //{
        //    try
        //    {
        //        var chart = GetChart();
        //        if (chart == null)
        //            return;

        //        var data = chart.CreateData();

        //        // Update stop-loss line
        //        if (stopLossPrice > 0)
        //        {
        //            data.Group(time).Add(_stopLossLine, stopLossPrice);
        //        }

        //        // Update take-profit line
        //        if (takeProfitPrice > 0)
        //        {
        //            data.Group(time).Add(_takeProfitLine, takeProfitPrice);
        //        }

        //        chart.Draw(data);
        //    }
        //    catch (Exception ex)
        //    {
        //        LogError($"Error updating stop-loss/take-profit lines: {ex.Message}");
        //    }
        //}
        
        /// <summary>
        /// Обновление графика
        /// </summary>
        private void UpdateChart(ICandleMessage candle)
        {
            try
            {
                var chart = GetChart();
                if (chart == null)
                    return;

                var data = chart.CreateData();
                var group = data.Group(candle.OpenTime);

                // Добавляем данные на график
                group.Add(_candleElement, candle);

                // получаем объекты IIndicatorValue непосредственно из индикаторов

                // Добавляем индикаторы, если они сформированы
                if (_fastEma.IsFormed)
                {
                    var fastEmaValue = _fastEma.Process(candle);
                    group.Add(_fastEmaElement, fastEmaValue);
                }

                if (_slowEma.IsFormed)
                {
                    var slowEmaValue = _slowEma.Process(candle);
                    group.Add(_slowEmaElement, slowEmaValue);
                }

                if (_longEma.IsFormed)
                {
                    var longEmaValue = _longEma.Process(candle);
                    group.Add(_longEmaElement, longEmaValue);
                }

                if (_rsi.IsFormed)
                {
                    var rsiValue = _rsi.Process(candle);
                    group.Add(_rsiElement, rsiValue);
                }

                if (_bollingerBands.IsFormed)
                {
                    var bollingerValue = _bollingerBands.Process(candle);
                    //group.Add(_bollingerMiddleElement, bollingerValue);

                    // Для верхней и нижней полос используйте соответствующие значения
                    if (_bollingerBands.UpBand != null && _bollingerBands.LowBand != null)
                    {
                        var upBandValue = _bollingerBands.UpBand.Process(candle);
                        var lowBandValue = _bollingerBands.LowBand.Process(candle);

                        group.Add(_bollingerUpperElement, upBandValue);
                        group.Add(_bollingerLowerElement, lowBandValue);
                    }
                }

                // Рисуем данные
                chart.Draw(data);
            }
            catch (Exception ex)
            {
                // Ошибки визуализации не критичны для работы стратегии
                LogError($"Ошибка при обновлении графика: {ex.Message}");
            }
        }

        /// <summary>
        /// Обновление графика с отображением уровней стоп-лосса и тейк-профита
        /// </summary>
        //private void UpdateChartWithLevels(ICandleMessage candle, decimal stopLossPrice = 0, decimal takeProfitPrice = 0)
        //{
        //    try
        //    {
        //        var chart = GetChart();
        //        if (chart == null)
        //            return;

        //        var data = chart.CreateData();
        //        var group = data.Group(candle.OpenTime);

        //        // Добавляем свечи на график
        //        group.Add(_candleElement, candle);

        //        // Добавляем индикаторы, если они сформированы
        //        if (_fastEma.IsFormed)
        //        {
        //            var fastEmaValue = _fastEma.Process(candle);
        //            group.Add(_fastEmaElement, fastEmaValue);
        //        }

        //        // Аналогично для других индикаторов...

        //        // Для отображения уровней стоп-лосса и тейк-профита можно использовать текстовую аннотацию
        //        // или создать специальные элементы в отдельном методе

        //        // Рисуем данные
        //        chart.Draw(data);
        //    }
        //    catch (Exception ex)
        //    {
        //        // Ошибки визуализации не критичны для работы стратегии
        //        LogError($"Ошибка при обновлении графика: {ex.Message}");
        //    }
        //}
    }
}