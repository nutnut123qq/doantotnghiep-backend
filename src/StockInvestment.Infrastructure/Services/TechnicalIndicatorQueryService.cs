using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using StockInvestment.Application.Interfaces;
using StockInvestment.Domain.Entities;
using StockInvestment.Infrastructure.Data;

namespace StockInvestment.Infrastructure.Services;

public class TechnicalIndicatorQueryService : ITechnicalIndicatorQueryService
{
    private readonly ApplicationDbContext _dbContext;
    private readonly ILogger<TechnicalIndicatorQueryService> _logger;

    private static readonly string[] DisplayOrder = ["MA20", "MA50", "RSI", "MACD"];

    public TechnicalIndicatorQueryService(
        ApplicationDbContext dbContext,
        ILogger<TechnicalIndicatorQueryService> logger)
    {
        _dbContext = dbContext;
        _logger = logger;
    }

    public async Task<IReadOnlyList<TechnicalIndicator>> GetLatestStoredIndicatorsAsync(
        string symbol,
        CancellationToken cancellationToken = default)
    {
        var normalized = symbol.Trim().ToUpperInvariant();

        var tickerId = await _dbContext.StockTickers.AsNoTracking()
            .Where(t => t.Symbol == normalized)
            .Select(t => t.Id)
            .FirstOrDefaultAsync(cancellationToken);

        if (tickerId == Guid.Empty)
        {
            _logger.LogDebug("No StockTicker row for symbol {Symbol}; stored indicators empty", normalized);
            return Array.Empty<TechnicalIndicator>();
        }

        var rows = await _dbContext.TechnicalIndicators.AsNoTracking()
            .Where(i => i.TickerId == tickerId)
            .ToListAsync(cancellationToken);

        if (rows.Count == 0)
        {
            return Array.Empty<TechnicalIndicator>();
        }

        // Detached copies without navigation property for JSON serialization
        var ordered = rows
            .Select(r => new TechnicalIndicator
            {
                Id = r.Id,
                TickerId = r.TickerId,
                IndicatorType = r.IndicatorType,
                Value = r.Value,
                TrendAssessment = r.TrendAssessment,
                CalculatedAt = r.CalculatedAt
            })
            .ToList();

        ordered.Sort((a, b) =>
        {
            var ia = Array.IndexOf(DisplayOrder, a.IndicatorType);
            var ib = Array.IndexOf(DisplayOrder, b.IndicatorType);
            if (ia < 0 && ib < 0) return string.Compare(a.IndicatorType, b.IndicatorType, StringComparison.Ordinal);
            if (ia < 0) return 1;
            if (ib < 0) return -1;
            return ia.CompareTo(ib);
        });

        return ordered;
    }
}
