namespace SuperStrategy
{
    using System;
    using StockSharp.Messages;
    using StockSharp.Charting;
    using StockSharp.Algo.Indicators;
    using Newtonsoft.Json.Linq;

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
        private IChart _chart;

        /// <summary>
        /// Инициализация графика
        /// </summary>
        private void InitializeChart()
        {
            try
            {
                // Инициализация графика
                _chart = GetChart();

                if (_chart == null)
                {
                    LogInfo("Chart unavailable, visualization disabled.");
                    return;
                }

                // Create chart areas
                var priceArea = CreateChartArea();
                var indicatorArea = CreateChartArea();

                // Add elements to chart
                _candleElement = priceArea.AddCandles();
                _tradesElement = DrawOwnTrades(priceArea);
                
                // Add indicators
                _fastEmaElement = priceArea.AddIndicator(_fastEma);
                _fastEmaElement.Color = System.Drawing.Color.Blue;

                _slowEmaElement = priceArea.AddIndicator(_slowEma);
                _slowEmaElement.Color = System.Drawing.Color.Red;

                _longEmaElement = priceArea.AddIndicator(_longEma);
                _longEmaElement.Color = System.Drawing.Color.Green;

                //_bollingerUpperElement = priceArea.AddIndicator(_bollingerBands.UpBand);
                //_bollingerLowerElement = priceArea.AddIndicator(_bollingerBands.LowBand);

                _rsiElement = indicatorArea.AddIndicator(_rsi);

                LogInfo("Chart initialized successfully.");
            }
            catch (Exception ex)
            {
                LogErrorDetailed("Error initializing chart", ex);
            }
        }
        
        /// <summary>
        /// Обновление графика
        /// </summary>
        private void UpdateChart(ICandleMessage candle)
        {
            try
            {
                if (_chart == null)
                    return;
                
                var data = _chart.CreateData();
                var group = data.Group(candle.OpenTime);
               
                
                // Добавляем данные на график
                group.Add(_candleElement, candle);
                group.Add(_slowEmaElement, slowEmaValue);
                group.Add(_rsiElement, rsiValue);
                group.Add(_fastEmaElement, fastEmaValue);
                

                //if (_bollingerBands.IsFormed)
                //{
                //    // Для верхней и нижней полос используйте соответствующие значения
                //    if (_bollingerBands.UpBand != null && _bollingerBands.LowBand != null)
                //    {
                //        var upBandValue = _bollingerBands.UpBand.GetCurrentValue<IIndicatorValue>();
                //        var lowBandValue = _bollingerBands.LowBand.GetCurrentValue<IIndicatorValue>();

                //        group.Add(_bollingerUpperElement, upBandValue);
                //        group.Add(_bollingerLowerElement, lowBandValue);
                //    }
                //}

                // Рисуем данные
                _chart.Draw(data);
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