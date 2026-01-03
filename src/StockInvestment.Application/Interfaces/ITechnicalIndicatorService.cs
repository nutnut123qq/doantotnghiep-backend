using StockInvestment.Domain.Entities;

namespace StockInvestment.Application.Interfaces;

/// <summary>
/// Service for calculating technical indicators
/// </summary>
public interface ITechnicalIndicatorService
{
    /// <summary>
    /// Calculate Moving Average (MA)
    /// </summary>
    Task<decimal> CalculateMAAsync(string symbol, int period = 20);

    /// <summary>
    /// Calculate Relative Strength Index (RSI)
    /// </summary>
    Task<decimal> CalculateRSIAsync(string symbol, int period = 14);

    /// <summary>
    /// Calculate MACD (Moving Average Convergence Divergence)
    /// </summary>
    Task<MACDResult> CalculateMACDAsync(string symbol, int fastPeriod = 12, int slowPeriod = 26, int signalPeriod = 9);

    /// <summary>
    /// Calculate all indicators for a symbol
    /// </summary>
    Task<List<TechnicalIndicator>> CalculateAllIndicatorsAsync(string symbol);

    /// <summary>
    /// Get trend assessment based on indicators
    /// </summary>
    string GetTrendAssessment(decimal rsi, MACDResult macd);
}

public class MACDResult
{
    public decimal MACD { get; set; }
    public decimal Signal { get; set; }
    public decimal Histogram { get; set; }
}

