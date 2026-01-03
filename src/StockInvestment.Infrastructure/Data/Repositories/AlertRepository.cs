using Microsoft.EntityFrameworkCore;
using StockInvestment.Application.Interfaces;
using StockInvestment.Domain.Entities;

namespace StockInvestment.Infrastructure.Data.Repositories;

public class AlertRepository : Repository<Alert>, IAlertRepository
{
    public AlertRepository(ApplicationDbContext context) : base(context)
    {
    }

    public async Task<IEnumerable<Alert>> GetByUserIdWithTickerAsync(Guid userId, bool? isActive = null, CancellationToken cancellationToken = default)
    {
        var query = _dbSet
            .Include(a => a.Ticker)
            .Where(a => a.UserId == userId);

        if (isActive.HasValue)
        {
            query = query.Where(a => a.IsActive == isActive.Value);
        }

        return await query
            .OrderByDescending(a => a.CreatedAt)
            .ToListAsync(cancellationToken);
    }

    public async Task<IEnumerable<Alert>> GetActiveAlertsWithTickerAndUserAsync(CancellationToken cancellationToken = default)
    {
        return await _dbSet
            .Include(a => a.Ticker)
            .Include(a => a.User)
            .Where(a => a.IsActive && a.TickerId != null)
            .ToListAsync(cancellationToken);
    }
}

