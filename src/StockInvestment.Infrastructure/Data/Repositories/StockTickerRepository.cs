using Microsoft.EntityFrameworkCore;
using StockInvestment.Application.Interfaces;
using StockInvestment.Domain.Entities;
using StockInvestment.Domain.Enums;
using StockInvestment.Infrastructure.Data;

namespace StockInvestment.Infrastructure.Data.Repositories;

/// <summary>
/// Repository implementation for Stock Tickers
/// </summary>
public class StockTickerRepository : Repository<StockTicker>, IStockTickerRepository
{
    public StockTickerRepository(ApplicationDbContext context) : base(context)
    {
    }

    public async Task<StockTicker?> GetBySymbolAsync(string symbol, CancellationToken cancellationToken = default)
    {
        return await _dbSet
            .FirstOrDefaultAsync(t => t.Symbol == symbol.ToUpper(), cancellationToken);
    }

    public async Task<Dictionary<string, StockTicker>> GetBySymbolsAsync(IEnumerable<string> symbols, CancellationToken cancellationToken = default)
    {
        var symbolsList = symbols.Select(s => s.ToUpper()).Distinct().ToList();
        if (!symbolsList.Any())
        {
            return new Dictionary<string, StockTicker>();
        }

        var tickers = await _dbSet
            .Where(t => symbolsList.Contains(t.Symbol))
            .ToListAsync(cancellationToken);

        return tickers.ToDictionary(t => t.Symbol, t => t);
    }

    public async Task<IEnumerable<StockTicker>> GetTickersAsync(
        string? exchange = null,
        string? index = null,
        string? industry = null,
        CancellationToken cancellationToken = default)
    {
        var query = _dbSet.AsQueryable();

        if (!string.IsNullOrEmpty(exchange))
        {
            if (Enum.TryParse<Exchange>(exchange, true, out var exchangeEnum))
            {
                query = query.Where(t => t.Exchange == exchangeEnum);
            }
        }

        if (!string.IsNullOrEmpty(industry))
        {
            query = query.Where(t => t.Industry != null && t.Industry.Contains(industry));
        }

        return await query
            .OrderBy(t => t.Symbol)
            .ToListAsync(cancellationToken);
    }
}
