using Microsoft.Extensions.Hosting;

namespace StockInvestment.Infrastructure.Services;

/// <summary>
/// Centralized cache key generator service
/// </summary>
public class CacheKeyGenerator : StockInvestment.Application.Interfaces.ICacheKeyGenerator
{
    private const string CacheVersion = "v1";
    private const string InsightsPrefix = "insights";
    private const string MarketSentimentPrefix = "market_sentiment";
    private const string OHLCVPrefix = "ohlcv";
    private const string QuotePrefix = "quote";
    private const string ForecastPrefix = "forecast";
    private const string PortfolioPrefix = "portfolio";
    private const string TickerPrefix = "ticker";
    
    private readonly string _environmentPrefix;

    public CacheKeyGenerator(IHostEnvironment environment)
    {
        _environmentPrefix = environment.EnvironmentName.ToLowerInvariant();
    }

    public string GenerateInsightsKey(string? type = null, string? symbol = null, bool includeDismissed = false)
    {
        var parts = new List<string> 
        { 
            _environmentPrefix,
            CacheVersion,
            InsightsPrefix 
        };
        
        if (!string.IsNullOrEmpty(type))
            parts.Add(type.ToLowerInvariant());
        else
            parts.Add("all");
            
        if (!string.IsNullOrEmpty(symbol))
            parts.Add(symbol.ToUpperInvariant());
        else
            parts.Add("all");
            
        parts.Add(includeDismissed ? "dismissed" : "active");
        
        return string.Join(":", parts);
    }

    public string GenerateMarketSentimentKey()
    {
        return $"{_environmentPrefix}:{CacheVersion}:{MarketSentimentPrefix}";
    }

    public string GenerateOHLCVKey(string symbol, DateTime startDate, DateTime endDate)
    {
        // Use UTC and invariant culture for consistent formatting
        var start = startDate.ToUniversalTime().ToString("yyyyMMdd", System.Globalization.CultureInfo.InvariantCulture);
        var end = endDate.ToUniversalTime().ToString("yyyyMMdd", System.Globalization.CultureInfo.InvariantCulture);
        return $"{_environmentPrefix}:{CacheVersion}:{OHLCVPrefix}:{symbol.ToUpperInvariant()}:{start}:{end}";
    }

    public string GenerateQuoteKey(string symbol)
    {
        return $"{_environmentPrefix}:{CacheVersion}:{QuotePrefix}:{symbol.ToUpperInvariant()}";
    }

    public string GenerateForecastKey(string symbol, string timeHorizon)
    {
        return $"{_environmentPrefix}:{CacheVersion}:{ForecastPrefix}:{symbol.ToUpperInvariant()}:{timeHorizon.ToLowerInvariant()}";
    }

    public string GeneratePortfolioHoldingsKey(Guid userId)
    {
        return $"{_environmentPrefix}:{CacheVersion}:{PortfolioPrefix}:holdings:{userId}";
    }

    public string GeneratePortfolioSummaryKey(Guid userId)
    {
        return $"{_environmentPrefix}:{CacheVersion}:{PortfolioPrefix}:summary:{userId}";
    }

    public string GenerateTickerKey(string symbol)
    {
        // Fix: Use colon instead of underscore for consistency
        return $"{_environmentPrefix}:{CacheVersion}:{TickerPrefix}:{symbol.ToUpperInvariant()}";
    }

    public string GeneratePattern(string prefix)
    {
        return $"{_environmentPrefix}:{CacheVersion}:{prefix}:*";
    }
}
