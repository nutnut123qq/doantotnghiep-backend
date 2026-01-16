namespace StockInvestment.Domain.Constants;

/// <summary>
/// Constants for market sentiment calculations
/// </summary>
public static class MarketSentimentConstants
{
    /// <summary>
    /// Default neutral sentiment score
    /// </summary>
    public const int DEFAULT_SENTIMENT_SCORE = 50;

    /// <summary>
    /// High volatility index for high risk
    /// </summary>
    public const int HIGH_VOLATILITY_INDEX = 80;

    /// <summary>
    /// Moderate volatility index
    /// </summary>
    public const int MODERATE_VOLATILITY_INDEX = 50;

    /// <summary>
    /// Low volatility index
    /// </summary>
    public const int LOW_VOLATILITY_INDEX = 20;
}
