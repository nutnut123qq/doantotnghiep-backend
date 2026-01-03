using Microsoft.Extensions.Logging;
using StockInvestment.Application.Interfaces;
using StockInvestment.Domain.Entities;

namespace StockInvestment.Infrastructure.Services;

public class TechnicalIndicatorService : ITechnicalIndicatorService
{
    private readonly IVNStockService _vnStockService;
    private readonly ILogger<TechnicalIndicatorService> _logger;

    public TechnicalIndicatorService(
        IVNStockService vnStockService,
        ILogger<TechnicalIndicatorService> logger)
    {
        _vnStockService = vnStockService;
        _logger = logger;
    }

    public async Task<decimal> CalculateMAAsync(string symbol, int period = 20)
    {
        try
        {
            var endDate = DateTime.Now;
            var startDate = endDate.AddDays(-(period + 10)); // Extra days for calculation

            var historicalData = await _vnStockService.GetHistoricalDataAsync(symbol, startDate, endDate);
            var dataList = historicalData.ToList();

            if (dataList.Count < period)
            {
                _logger.LogWarning("Not enough data to calculate MA for {Symbol}", symbol);
                return 0;
            }

            var recentData = dataList.TakeLast(period);
            var ma = recentData.Average(d => d.Close);

            return ma;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error calculating MA for {Symbol}", symbol);
            return 0;
        }
    }

    public async Task<decimal> CalculateRSIAsync(string symbol, int period = 14)
    {
        try
        {
            var endDate = DateTime.Now;
            var startDate = endDate.AddDays(-(period + 20));

            var historicalData = await _vnStockService.GetHistoricalDataAsync(symbol, startDate, endDate);
            var dataList = historicalData.OrderBy(d => d.Date).ToList();

            if (dataList.Count < period + 1)
            {
                _logger.LogWarning("Not enough data to calculate RSI for {Symbol}", symbol);
                return 50; // Neutral
            }

            var gains = new List<decimal>();
            var losses = new List<decimal>();

            for (int i = 1; i < dataList.Count; i++)
            {
                var change = dataList[i].Close - dataList[i - 1].Close;
                if (change > 0)
                {
                    gains.Add(change);
                    losses.Add(0);
                }
                else
                {
                    gains.Add(0);
                    losses.Add(Math.Abs(change));
                }
            }

            var avgGain = gains.TakeLast(period).Average();
            var avgLoss = losses.TakeLast(period).Average();

            if (avgLoss == 0)
                return 100;

            var rs = avgGain / avgLoss;
            var rsi = 100 - (100 / (1 + rs));

            return rsi;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error calculating RSI for {Symbol}", symbol);
            return 50;
        }
    }

    public async Task<MACDResult> CalculateMACDAsync(string symbol, int fastPeriod = 12, int slowPeriod = 26, int signalPeriod = 9)
    {
        try
        {
            var endDate = DateTime.Now;
            var startDate = endDate.AddDays(-(slowPeriod + signalPeriod + 20));

            var historicalData = await _vnStockService.GetHistoricalDataAsync(symbol, startDate, endDate);
            var dataList = historicalData.OrderBy(d => d.Date).ToList();

            if (dataList.Count < slowPeriod + signalPeriod)
            {
                _logger.LogWarning("Not enough data to calculate MACD for {Symbol}", symbol);
                return new MACDResult();
            }

            // Calculate EMAs
            var fastEMA = CalculateEMA(dataList.Select(d => d.Close).ToList(), fastPeriod);
            var slowEMA = CalculateEMA(dataList.Select(d => d.Close).ToList(), slowPeriod);

            var macdLine = fastEMA - slowEMA;

            // Calculate signal line (EMA of MACD)
            var macdValues = new List<decimal> { macdLine };
            var signalLine = macdLine; // Simplified - should calculate EMA of MACD values

            var histogram = macdLine - signalLine;

            return new MACDResult
            {
                MACD = macdLine,
                Signal = signalLine,
                Histogram = histogram
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error calculating MACD for {Symbol}", symbol);
            return new MACDResult();
        }
    }

    public async Task<List<TechnicalIndicator>> CalculateAllIndicatorsAsync(string symbol)
    {
        var indicators = new List<TechnicalIndicator>();

        try
        {
            var ma20 = await CalculateMAAsync(symbol, 20);
            var ma50 = await CalculateMAAsync(symbol, 50);
            var rsi = await CalculateRSIAsync(symbol, 14);
            var macd = await CalculateMACDAsync(symbol);

            var trend = GetTrendAssessment(rsi, macd);

            indicators.Add(new TechnicalIndicator
            {
                IndicatorType = "MA20",
                Value = ma20,
                TrendAssessment = trend,
                CalculatedAt = DateTime.UtcNow
            });

            indicators.Add(new TechnicalIndicator
            {
                IndicatorType = "MA50",
                Value = ma50,
                TrendAssessment = trend,
                CalculatedAt = DateTime.UtcNow
            });

            indicators.Add(new TechnicalIndicator
            {
                IndicatorType = "RSI",
                Value = rsi,
                TrendAssessment = GetRSITrend(rsi),
                CalculatedAt = DateTime.UtcNow
            });

            indicators.Add(new TechnicalIndicator
            {
                IndicatorType = "MACD",
                Value = macd.MACD,
                TrendAssessment = macd.Histogram > 0 ? "Bullish" : "Bearish",
                CalculatedAt = DateTime.UtcNow
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error calculating all indicators for {Symbol}", symbol);
        }

        return indicators;
    }

    public string GetTrendAssessment(decimal rsi, MACDResult macd)
    {
        var bullishSignals = 0;
        var bearishSignals = 0;

        if (rsi > 70) bearishSignals++;
        else if (rsi < 30) bullishSignals++;
        else if (rsi > 50) bullishSignals++;
        else bearishSignals++;

        if (macd.Histogram > 0) bullishSignals++;
        else bearishSignals++;

        if (bullishSignals > bearishSignals) return "Bullish";
        if (bearishSignals > bullishSignals) return "Bearish";
        return "Neutral";
    }

    private string GetRSITrend(decimal rsi)
    {
        if (rsi > 70) return "Overbought";
        if (rsi < 30) return "Oversold";
        if (rsi > 50) return "Bullish";
        return "Bearish";
    }

    private decimal CalculateEMA(List<decimal> prices, int period)
    {
        if (prices.Count < period)
            return 0;

        var multiplier = 2m / (period + 1);
        var ema = prices.Take(period).Average();

        foreach (var price in prices.Skip(period))
        {
            ema = (price - ema) * multiplier + ema;
        }

        return ema;
    }
}
