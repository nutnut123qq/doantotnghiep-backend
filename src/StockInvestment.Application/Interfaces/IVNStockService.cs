using StockInvestment.Domain.Entities;

namespace StockInvestment.Application.Interfaces;

/// <summary>
/// Interface for VNStock API integration
/// </summary>
public interface IVNStockService
{
    /// <summary>
    /// Get all stock symbols from VNStock
    /// </summary>
    Task<IEnumerable<StockTicker>> GetAllSymbolsAsync(string? exchange = null);

    /// <summary>
    /// Get real-time quote for a specific symbol
    /// </summary>
    Task<StockTicker?> GetQuoteAsync(string symbol);

    /// <summary>
    /// Get real-time quotes for multiple symbols
    /// </summary>
    Task<IEnumerable<StockTicker>> GetQuotesAsync(IEnumerable<string> symbols);

    /// <summary>
    /// Get historical OHLCV data for a symbol
    /// </summary>
    Task<IEnumerable<OHLCVData>> GetHistoricalDataAsync(string symbol, DateTime startDate, DateTime endDate);
}

/// <summary>
/// OHLCV (Open, High, Low, Close, Volume) data model
/// </summary>
public class OHLCVData
{
    public DateTime Date { get; set; }
    public decimal Open { get; set; }
    public decimal High { get; set; }
    public decimal Low { get; set; }
    public decimal Close { get; set; }
    public long Volume { get; set; }
}

