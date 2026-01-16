namespace StockInvestment.Application.Interfaces;

/// <summary>
/// Interface for generating standardized cache keys
/// </summary>
public interface ICacheKeyGenerator
{
    /// <summary>
    /// Generate cache key for AI insights
    /// </summary>
    string GenerateInsightsKey(string? type = null, string? symbol = null, bool includeDismissed = false);

    /// <summary>
    /// Generate cache key for market sentiment
    /// </summary>
    string GenerateMarketSentimentKey();

    /// <summary>
    /// Generate cache key for OHLCV data
    /// </summary>
    string GenerateOHLCVKey(string symbol, DateTime startDate, DateTime endDate);

    /// <summary>
    /// Generate cache key for quote data
    /// </summary>
    string GenerateQuoteKey(string symbol);

    /// <summary>
    /// Generate cache key for forecast
    /// </summary>
    string GenerateForecastKey(string symbol, string timeHorizon);

    /// <summary>
    /// Generate cache key for portfolio holdings
    /// </summary>
    string GeneratePortfolioHoldingsKey(Guid userId);

    /// <summary>
    /// Generate cache key for portfolio summary
    /// </summary>
    string GeneratePortfolioSummaryKey(Guid userId);

    /// <summary>
    /// Generate cache key for ticker
    /// </summary>
    string GenerateTickerKey(string symbol);

    /// <summary>
    /// Generate pattern for invalidating related cache keys
    /// </summary>
    string GeneratePattern(string prefix);
}
