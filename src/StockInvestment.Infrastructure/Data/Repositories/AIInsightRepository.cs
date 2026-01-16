using Microsoft.EntityFrameworkCore;
using StockInvestment.Application.Interfaces;
using StockInvestment.Domain.Entities;
using StockInvestment.Domain.Enums;
using StockInvestment.Infrastructure.Data;

namespace StockInvestment.Infrastructure.Data.Repositories;

/// <summary>
/// Repository implementation for AI Insights
/// </summary>
public class AIInsightRepository : Repository<AIInsight>, IAIInsightRepository
{
    public AIInsightRepository(ApplicationDbContext context) : base(context)
    {
    }

    public async Task<IEnumerable<AIInsight>> GetInsightsAsync(
        InsightType? type = null,
        string? symbol = null,
        bool includeDismissed = false,
        CancellationToken cancellationToken = default)
    {
        var query = _dbSet
            .Include(i => i.Ticker)
            .AsQueryable();

        if (type.HasValue)
        {
            query = query.Where(i => i.Type == type.Value);
        }

        if (!string.IsNullOrEmpty(symbol))
        {
            query = query.Where(i => i.Ticker.Symbol == symbol.ToUpper());
        }

        if (!includeDismissed)
        {
            query = query.Where(i => i.DismissedAt == null);
        }

        return await query
            .OrderByDescending(i => i.GeneratedAt)
            .ToListAsync(cancellationToken);
    }

    public async Task<AIInsight?> GetInsightByIdWithTickerAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await _dbSet
            .Include(i => i.Ticker)
            .FirstOrDefaultAsync(i => i.Id == id, cancellationToken);
    }

    public async Task<AIInsight?> FindActiveInsightAsync(
        Guid tickerId,
        InsightType type,
        CancellationToken cancellationToken = default)
    {
        return await _dbSet
            .Include(i => i.Ticker)
            .FirstOrDefaultAsync(
                i => i.TickerId == tickerId && 
                     i.Type == type && 
                     i.DismissedAt == null,
                cancellationToken);
    }

    public async Task<IEnumerable<AIInsight>> GetNonDismissedInsightsAsync(CancellationToken cancellationToken = default)
    {
        return await _dbSet
            .Where(i => i.DismissedAt == null)
            .ToListAsync(cancellationToken);
    }

    public async Task<IEnumerable<AIInsight>> GetOldDismissedInsightsAsync(DateTime cutoffDate, CancellationToken cancellationToken = default)
    {
        return await _dbSet
            .Where(i => i.DismissedAt != null && i.DismissedAt < cutoffDate)
            .ToListAsync(cancellationToken);
    }
}
