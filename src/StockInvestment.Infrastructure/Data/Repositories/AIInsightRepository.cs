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
        bool includeDeleted = false,
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

        if (!includeDeleted)
        {
            query = query.Where(i => !i.IsDeleted);
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
                     i.DismissedAt == null &&
                     !i.IsDeleted,
                cancellationToken);
    }

    public async Task<IEnumerable<AIInsight>> GetNonDismissedInsightsAsync(CancellationToken cancellationToken = default)
    {
        return await _dbSet
            .Where(i => i.DismissedAt == null && !i.IsDeleted)
            .ToListAsync(cancellationToken);
    }

    public async Task<IEnumerable<AIInsight>> GetOldDismissedInsightsAsync(DateTime cutoffDate, CancellationToken cancellationToken = default)
    {
        return await _dbSet
            .Where(i => i.DismissedAt != null && i.DismissedAt < cutoffDate)
            .ToListAsync(cancellationToken);
    }

    public async Task<DateTime?> GetLatestGeneratedAtByTickerAsync(Guid tickerId, CancellationToken cancellationToken = default)
    {
        return await _dbSet
            .Where(i => i.TickerId == tickerId && i.DismissedAt == null && !i.IsDeleted)
            .OrderByDescending(i => i.GeneratedAt)
            .Select(i => (DateTime?)i.GeneratedAt)
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<IEnumerable<AIInsight>> GetGlobalLatestInsightsAsync(
        InsightType? type = null,
        string? symbol = null,
        bool includeDeleted = false,
        CancellationToken cancellationToken = default)
    {
        var baseQuery = _dbSet
            .Include(i => i.Ticker)
            .Where(i => i.DismissedAt == null)
            .AsQueryable();

        if (!includeDeleted)
        {
            baseQuery = baseQuery.Where(i => !i.IsDeleted);
        }

        if (type.HasValue)
        {
            baseQuery = baseQuery.Where(i => i.Type == type.Value);
        }

        if (!string.IsNullOrWhiteSpace(symbol))
        {
            baseQuery = baseQuery.Where(i => i.Ticker.Symbol == symbol.ToUpper());
        }

        var latestIds = await baseQuery
            .GroupBy(i => i.TickerId)
            .Select(g => g
                .OrderByDescending(x => x.GeneratedAt)
                .Select(x => x.Id)
                .First())
            .ToListAsync(cancellationToken);

        return await _dbSet
            .Include(i => i.Ticker)
            .Where(i => latestIds.Contains(i.Id))
            .OrderByDescending(i => i.GeneratedAt)
            .ThenByDescending(i => i.Confidence)
            .ToListAsync(cancellationToken);
    }
}
