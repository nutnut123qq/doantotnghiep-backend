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
            var endDate = DateTime.UtcNow;
            var startDate = endDate.AddDays(-(period + 10)); // Extra days for calculation

            var historicalData = await _vnStockService.GetHistoricalDataAsync(symbol, startDate, endDate);
            var dataList = historicalData.OrderBy(d => d.Date).ToList();

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
            var endDate = DateTime.UtcNow;
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
            var endDate = DateTime.UtcNow;
            var startDate = endDate.AddDays(-(slowPeriod + signalPeriod + 30));

            var historicalData = await _vnStockService.GetHistoricalDataAsync(symbol, startDate, endDate);
            var dataList = historicalData.OrderBy(d => d.Date).ToList();

            // 1. Check insufficient data (cần +1 để tránh edge case)
            var prices = dataList.Select(d => d.Close).ToList();
            if (prices.Count < slowPeriod + signalPeriod + 1)
            {
                _logger.LogWarning("Not enough data to calculate MACD for {Symbol}. Need at least {Required} points",
                    symbol, slowPeriod + signalPeriod + 1);
                return new MACDResult();
            }

            // 2. Tính chuỗi EMA với startIndex
            var (fastStart, fastEMA) = CalculateEMASeries(prices, fastPeriod);
            var (slowStart, slowEMA) = CalculateEMASeries(prices, slowPeriod);

            // Guard: nếu EMA trả -1 hoặc empty (period > prices.Count)
            if (fastStart < 0 || slowStart < 0 || fastEMA.Count == 0 || slowEMA.Count == 0)
            {
                _logger.LogWarning("Failed to calculate EMA series for {Symbol}", symbol);
                return new MACDResult();
            }

            // 3. Align series từ max(fastStart, slowStart)
            var start = Math.Max(fastStart, slowStart); // thường = 25
            var macdSeries = new List<decimal>();

            for (int idx = start; idx < prices.Count; idx++)
            {
                var fastVal = fastEMA[idx - fastStart];  // align đúng
                var slowVal = slowEMA[idx - slowStart];
                macdSeries.Add(fastVal - slowVal);
            }

            // 4. Tính Signal = EMA9 của MACD
            // Check đủ dữ liệu cho signal trước khi tính
            if (macdSeries.Count < signalPeriod)
            {
                _logger.LogWarning("Not enough MACD data to calculate signal for {Symbol}", symbol);
                return new MACDResult();
            }

            var (_, signalSeries) = CalculateEMASeries(macdSeries, signalPeriod);

            if (signalSeries.Count == 0)
            {
                _logger.LogWarning("Failed to calculate signal for {Symbol}", symbol);
                return new MACDResult();
            }

            // 5. Lấy giá trị cuối
            var macdLine = macdSeries.Last();
            var signalLine = signalSeries.Last();
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
        try
        {
            var ma20 = await CalculateMAAsync(symbol, 20);
            var ma50 = await CalculateMAAsync(symbol, 50);
            var rsi = await CalculateRSIAsync(symbol, 14);
            var macd = await CalculateMACDAsync(symbol);

            return BuildIndicatorList(ma20, ma50, rsi, macd);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error calculating all indicators for {Symbol}", symbol);
            return new List<TechnicalIndicator>();
        }
    }

    private List<TechnicalIndicator> BuildIndicatorList(decimal ma20, decimal ma50, decimal rsi, MACDResult macd)
    {
        var indicators = new List<TechnicalIndicator>();
        var trend = GetTrendAssessment(rsi, macd);

        // MACD trend với epsilon để tránh coi histogram ≈ 0 là Bearish
        var macdTrend = macd.Histogram > 0.0001m ? "Bullish" :
                        macd.Histogram < -0.0001m ? "Bearish" : "Neutral";

        indicators.Add(CreateIndicatorEntity("MA20", ma20, trend));
        indicators.Add(CreateIndicatorEntity("MA50", ma50, trend));
        indicators.Add(CreateIndicatorEntity("RSI", rsi, GetRSITrend(rsi)));
        indicators.Add(CreateIndicatorEntity("MACD", macd.MACD, macdTrend));

        return indicators;
    }

    private TechnicalIndicator CreateIndicatorEntity(string indicatorType, decimal value, string trendAssessment)
    {
        return new TechnicalIndicator
        {
            IndicatorType = indicatorType,
            Value = value,
            TrendAssessment = trendAssessment,
            CalculatedAt = DateTime.UtcNow
        };
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

    private (int startIndex, List<decimal> values) CalculateEMASeries(List<decimal> prices, int period)
    {
        if (prices.Count < period)
            return (-1, new List<decimal>());

        var multiplier = 2m / (period + 1);
        var ema = prices.Take(period).Average(); // EMA initial = SMA
        var values = new List<decimal> { ema };

        for (int i = period; i < prices.Count; i++)
        {
            ema = (prices[i] - ema) * multiplier + ema;
            values.Add(ema);
        }

        // startIndex = period - 1: EMA bắt đầu từ điểm này trong timeline gốc
        // VD: EMA12 bắt đầu từ index 11, EMA26 từ index 25
        return (period - 1, values);
    }
}
