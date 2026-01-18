using Microsoft.EntityFrameworkCore;
using StockInvestment.Application.Interfaces;
using StockInvestment.Domain.Entities;

namespace StockInvestment.Infrastructure.Data.Repositories;

/// <summary>
/// Repository implementation for Chart Settings
/// </summary>
public class ChartSettingsRepository : Repository<ChartSettings>, IChartSettingsRepository
{
    public ChartSettingsRepository(ApplicationDbContext context) : base(context)
    {
    }

    public async Task<ChartSettings?> GetByUserAndSymbolAsync(Guid userId, string symbol, CancellationToken cancellationToken = default)
    {
        return await _dbSet
            .FirstOrDefaultAsync(
                cs => cs.UserId == userId && cs.Symbol.ToLower() == symbol.ToLower(),
                cancellationToken);
    }

    public async Task<IEnumerable<ChartSettings>> GetByUserIdAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        return await _dbSet
            .Where(cs => cs.UserId == userId)
            .OrderByDescending(cs => cs.UpdatedAt)
            .ToListAsync(cancellationToken);
    }

    public async Task<ChartSettings> SaveSettingsAsync(ChartSettings settings, CancellationToken cancellationToken = default)
    {
        var existing = await GetByUserAndSymbolAsync(settings.UserId, settings.Symbol, cancellationToken);
        
        if (existing != null)
        {
            // Update existing
            existing.TimeRange = settings.TimeRange;
            existing.ChartType = settings.ChartType;
            existing.Indicators = settings.Indicators;
            existing.Drawings = settings.Drawings;
            existing.UpdatedAt = DateTime.UtcNow;
            
            _dbSet.Update(existing);
            return existing;
        }
        else
        {
            // Create new
            settings.UpdatedAt = DateTime.UtcNow;
            await _dbSet.AddAsync(settings, cancellationToken);
            return settings;
        }
    }
}
