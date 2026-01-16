namespace StockInvestment.Domain.Constants;

/// <summary>
/// Constants for technical indicator calculations
/// </summary>
public static class TechnicalIndicatorConstants
{
    /// <summary>
    /// RSI overbought threshold (typically 70)
    /// </summary>
    public const int RSI_OVERBOUGHT_THRESHOLD = 70;

    /// <summary>
    /// RSI oversold threshold (typically 30)
    /// </summary>
    public const int RSI_OVERSOLD_THRESHOLD = 30;

    /// <summary>
    /// Sentiment positive threshold
    /// </summary>
    public const double SENTIMENT_POSITIVE_THRESHOLD = 0.1;

    /// <summary>
    /// Sentiment negative threshold
    /// </summary>
    public const double SENTIMENT_NEGATIVE_THRESHOLD = -0.1;

    /// <summary>
    /// High confidence threshold for buy signals (70%)
    /// </summary>
    public const int HIGH_CONFIDENCE_THRESHOLD = 70;

    /// <summary>
    /// Bullish market threshold (60% buy signals)
    /// </summary>
    public const double BULLISH_MARKET_THRESHOLD = 60.0;

    /// <summary>
    /// Bearish market threshold (60% sell signals)
    /// </summary>
    public const double BEARISH_MARKET_THRESHOLD = 60.0;

    /// <summary>
    /// High risk threshold (40% sell signals)
    /// </summary>
    public const double HIGH_RISK_THRESHOLD = 40.0;

    /// <summary>
    /// Moderate risk threshold (20% sell signals)
    /// </summary>
    public const double MODERATE_RISK_THRESHOLD = 20.0;
}
