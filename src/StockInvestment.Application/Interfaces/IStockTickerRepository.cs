using StockInvestment.Domain.Entities;

namespace StockInvestment.Application.Interfaces;

/// <summary>
/// Repository interface for Stock Tickers
/// </summary>
public interface IStockTickerRepository : IRepository<StockTicker>
{
    /// <summary>
    /// Get ticker by symbol
    /// </summary>
    Task<StockTicker?> GetBySymbolAsync(string symbol, CancellationToken cancellationToken = default);

    /// <summary>
    /// Get multiple tickers by symbols
    /// </summary>
    Task<Dictionary<string, StockTicker>> GetBySymbolsAsync(IEnumerable<string> symbols, CancellationToken cancellationToken = default);

    /// <summary>
    /// Get tickers with filters
    /// </summary>
    Task<IEnumerable<StockTicker>> GetTickersAsync(
        string? exchange = null,
        string? index = null,
        string? industry = null,
        CancellationToken cancellationToken = default);
}
