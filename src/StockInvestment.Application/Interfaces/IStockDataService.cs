using StockInvestment.Domain.Entities;

namespace StockInvestment.Application.Interfaces;

public interface IStockDataService
{
    Task<IEnumerable<StockTicker>> GetTickersAsync(string? exchange = null, string? index = null, string? industry = null, Guid? watchlistId = null);
    Task<StockTicker?> GetTickerBySymbolAsync(string symbol);
    Task<StockTicker?> GetTickerByIdAsync(Guid id);
    Task<Dictionary<string, StockTicker>> GetTickersBySymbolsAsync(IEnumerable<string> symbols);
}

