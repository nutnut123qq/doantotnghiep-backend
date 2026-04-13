using StockInvestment.Application.Interfaces;
using StockInvestment.Domain.Entities;
using StockInvestment.Domain.Enums;

namespace StockInvestment.Api.Tests.Mocks;

/// <summary>
/// Stub IVNStockService for integration tests (no external HTTP calls).
/// </summary>
public sealed class MockVNStockService : IVNStockService
{
    public Task<IEnumerable<StockTicker>> GetAllSymbolsAsync(string? exchange = null)
        => Task.FromResult(Enumerable.Empty<StockTicker>());

    public Task<StockTicker?> GetQuoteAsync(string symbol)
        => Task.FromResult<StockTicker?>(new StockTicker { Symbol = symbol, Name = symbol, Exchange = Exchange.HOSE, CurrentPrice = 0 });

    public Task<IEnumerable<StockTicker>> GetQuotesAsync(IEnumerable<string> symbols)
        => Task.FromResult(Enumerable.Empty<StockTicker>());

    public Task<IEnumerable<OHLCVData>> GetHistoricalDataAsync(string symbol, DateTime startDate, DateTime endDate)
        => Task.FromResult(Enumerable.Empty<OHLCVData>());
}
