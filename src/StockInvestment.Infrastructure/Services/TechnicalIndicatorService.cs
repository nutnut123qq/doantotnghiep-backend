using Microsoft.Extensions.Logging;
using StockInvestment.Application.DTOs.StockData;
using StockInvestment.Application.Interfaces;
using StockInvestment.Domain.Constants;
using StockInvestment.Domain.Entities;

namespace StockInvestment.Infrastructure.Services;

public class TechnicalIndicatorService : ITechnicalIndicatorService
{
    private readonly IVNStockService _vnStockService;
    private readonly ICacheService _cacheService;
    private readonly ICacheKeyGenerator _cacheKeyGenerator;
    private readonly ILogger<TechnicalIndicatorService> _logger;
    private static readonly TimeSpan IndicatorCacheTtl = TimeSpan.FromMinutes(5);
    private static readonly TimeSpan HistoricalDataCacheTtl = TimeSpan.FromMinutes(2);
    private static readonly TimeSpan OhlcvApiCacheTtl = TimeSpan.FromMinutes(30);

    public TechnicalIndicatorService(
        IVNStockService vnStockService,
        ICacheService cacheService,
        ICacheKeyGenerator cacheKeyGenerator,
        ILogger<TechnicalIndicatorService> logger)
    {
        _vnStockService = vnStockService;
        _cacheService = cacheService;
        _cacheKeyGenerator = cacheKeyGenerator;
        _logger = logger;
    }

    public async Task<decimal> CalculateMAAsync(string symbol, int period = 20)
    {
        try
        {
            if (!Vn30Universe.Contains(symbol))
            {
                return 0;
            }

            var cacheKey = $"indicator:ma:{symbol.ToUpperInvariant()}:{period}";
            var cached = await _cacheService.GetAsync<CachedDecimal>(cacheKey);
            if (cached != null)
            {
                return cached.Value;
            }

            var endDate = DateTime.UtcNow;
            var startDate = endDate.AddDays(-(period + 10)); // Extra days for calculation

            var historicalData = await GetHistoricalDataCachedAsync(symbol, startDate, endDate);
            var dataList = historicalData.OrderBy(d => d.Date).ToList();

            if (dataList.Count < period)
            {
                _logger.LogWarning("Not enough data to calculate MA for {Symbol}", symbol);
                return 0;
            }

            var recentData = dataList.TakeLast(period);
            var ma = recentData.Average(d => d.Close);
            await _cacheService.SetAsync(cacheKey, new CachedDecimal(ma), IndicatorCacheTtl);

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
            if (!Vn30Universe.Contains(symbol))
            {
                return 50;
            }

            var cacheKey = $"indicator:rsi:{symbol.ToUpperInvariant()}:{period}";
            var cached = await _cacheService.GetAsync<CachedDecimal>(cacheKey);
            if (cached != null)
            {
                return cached.Value;
            }

            var endDate = DateTime.UtcNow;
            var startDate = endDate.AddDays(-(period + 20));

            var historicalData = await GetHistoricalDataCachedAsync(symbol, startDate, endDate);
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
            await _cacheService.SetAsync(cacheKey, new CachedDecimal(rsi), IndicatorCacheTtl);

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
            if (!Vn30Universe.Contains(symbol))
            {
                return new MACDResult();
            }

            var cacheKey = $"indicator:macd:{symbol.ToUpperInvariant()}:{fastPeriod}:{slowPeriod}:{signalPeriod}";
            var cached = await _cacheService.GetAsync<MACDResult>(cacheKey);
            if (cached != null)
            {
                return cached;
            }

            var endDate = DateTime.UtcNow;
            var startDate = endDate.AddDays(-(slowPeriod + signalPeriod + 30));

            var historicalData = await GetHistoricalDataCachedAsync(symbol, startDate, endDate);
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

            var result = new MACDResult
            {
                MACD = macdLine,
                Signal = signalLine,
                Histogram = histogram
            };
            await _cacheService.SetAsync(cacheKey, result, IndicatorCacheTtl);
            return result;
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
            if (!Vn30Universe.Contains(symbol))
            {
                return new List<TechnicalIndicator>();
            }

            var normalizedSymbol = symbol.ToUpperInvariant();
            var endDate = DateTime.UtcNow;
            var startDate = endDate.AddDays(-90);
            var historicalData = (await GetHistoricalDataCachedAsync(normalizedSymbol, startDate, endDate))
                .OrderBy(d => d.Date)
                .ToList();

            var ma20 = ComputeMAOrNull(historicalData, 20);
            var ma50 = ComputeMAOrNull(historicalData, 50);
            var rsi = ComputeRSIOrNull(historicalData, 14);
            var macd = ComputeMACDOrNull(historicalData, 12, 26, 9);

            return BuildIndicatorList(ma20, ma50, rsi, macd);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error calculating all indicators for {Symbol}", symbol);
            return new List<TechnicalIndicator>();
        }
    }

    private List<TechnicalIndicator> BuildIndicatorList(decimal? ma20, decimal? ma50, decimal? rsi, MACDResult? macd)
    {
        var indicators = new List<TechnicalIndicator>();
        var trend = GetTrendAssessmentOrNull(rsi, macd);

        // MACD trend với epsilon để tránh coi histogram ≈ 0 là Bearish
        var macdTrend = macd == null
            ? null
            : macd.Histogram > 0.0001m ? "Bullish" :
              macd.Histogram < -0.0001m ? "Bearish" : "Neutral";

        indicators.Add(CreateIndicatorEntity("MA20", ma20, trend));
        indicators.Add(CreateIndicatorEntity("MA50", ma50, trend));
        indicators.Add(CreateIndicatorEntity("RSI", rsi, rsi.HasValue ? GetRSITrend(rsi.Value) : null));
        indicators.Add(CreateIndicatorEntity("MACD", macd?.MACD, macdTrend));

        return indicators;
    }

    private TechnicalIndicator CreateIndicatorEntity(string indicatorType, decimal? value, string? trendAssessment)
    {
        return new TechnicalIndicator
        {
            IndicatorType = indicatorType,
            Value = value,
            TrendAssessment = trendAssessment,
            CalculatedAt = DateTime.UtcNow
        };
    }

    private string? GetTrendAssessmentOrNull(decimal? rsi, MACDResult? macd)
    {
        if (!rsi.HasValue || macd == null)
        {
            return null;
        }

        return GetTrendAssessment(rsi.Value, macd);
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

    private async Task<List<OHLCVData>> GetHistoricalDataCachedAsync(string symbol, DateTime startDate, DateTime endDate)
    {
        var cacheKey = $"historical:{symbol.ToUpperInvariant()}:{startDate:yyyyMMdd}:{endDate:yyyyMMdd}";
        var cached = await _cacheService.GetAsync<List<OHLCVData>>(cacheKey);
        if (cached != null)
        {
            return cached;
        }

        var data = (await _vnStockService.GetHistoricalDataAsync(symbol, startDate, endDate))
            .OrderBy(d => d.Date)
            .ToList();
        await _cacheService.SetAsync(cacheKey, data, HistoricalDataCacheTtl);

        var apiCacheKey = _cacheKeyGenerator.GenerateOHLCVKey(symbol, startDate, endDate);
        var apiPayload = data.Select(d => new OHLCVResponseDto
        {
            Time = new DateTimeOffset(d.Date).ToUnixTimeSeconds(),
            Open = d.Open,
            High = d.High,
            Low = d.Low,
            Close = d.Close,
            Volume = d.Volume
        }).ToList();
        await _cacheService.SetAsync(apiCacheKey, apiPayload, OhlcvApiCacheTtl);
        return data;
    }

    private decimal? ComputeMAOrNull(List<OHLCVData> data, int period)
    {
        if (data.Count < period) return null;
        return data.TakeLast(period).Average(d => d.Close);
    }

    private decimal? ComputeRSIOrNull(List<OHLCVData> data, int period)
    {
        if (data.Count < period + 1) return null;

        var gains = new List<decimal>();
        var losses = new List<decimal>();
        for (int i = 1; i < data.Count; i++)
        {
            var change = data[i].Close - data[i - 1].Close;
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

        var avgGain = gains.TakeLast(period).DefaultIfEmpty(0).Average();
        var avgLoss = losses.TakeLast(period).DefaultIfEmpty(0).Average();
        if (avgLoss == 0) return 100;
        var rs = avgGain / avgLoss;
        return 100 - (100 / (1 + rs));
    }

    private MACDResult? ComputeMACDOrNull(List<OHLCVData> data, int fastPeriod, int slowPeriod, int signalPeriod)
    {
        var prices = data.Select(d => d.Close).ToList();
        if (prices.Count < slowPeriod + signalPeriod + 1)
        {
            return null;
        }

        var (fastStart, fastEMA) = CalculateEMASeries(prices, fastPeriod);
        var (slowStart, slowEMA) = CalculateEMASeries(prices, slowPeriod);
        if (fastStart < 0 || slowStart < 0 || fastEMA.Count == 0 || slowEMA.Count == 0)
        {
            return null;
        }

        var start = Math.Max(fastStart, slowStart);
        var macdSeries = new List<decimal>();
        for (int idx = start; idx < prices.Count; idx++)
        {
            var fastVal = fastEMA[idx - fastStart];
            var slowVal = slowEMA[idx - slowStart];
            macdSeries.Add(fastVal - slowVal);
        }

        if (macdSeries.Count < signalPeriod)
        {
            return null;
        }

        var (_, signalSeries) = CalculateEMASeries(macdSeries, signalPeriod);
        if (signalSeries.Count == 0)
        {
            return null;
        }

        var macdLine = macdSeries.Last();
        var signalLine = signalSeries.Last();
        return new MACDResult
        {
            MACD = macdLine,
            Signal = signalLine,
            Histogram = macdLine - signalLine
        };
    }
}

public sealed class CachedDecimal
{
    public CachedDecimal(decimal value)
    {
        Value = value;
    }

    public decimal Value { get; }
}
