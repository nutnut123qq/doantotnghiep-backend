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

    /// <summary>
    /// P0-3: Atomically mark an alert as triggered using database-level atomic update
    /// This prevents duplicate notifications when multiple instances of AlertMonitorJob run concurrently
    /// </summary>
    public async Task<bool> TryMarkAsTriggeredAsync(Guid alertId, DateTime triggeredAt, CancellationToken cancellationToken = default)
    {
        // Use raw SQL for atomic UPDATE with WHERE conditions
        // Only updates if alert is still active and not already triggered
        // Returns the number of rows affected (1 if successful, 0 if already triggered or inactive)
        var rowsAffected = await _context.Database.ExecuteSqlRawAsync(
            @"UPDATE ""Alerts"" 
              SET ""IsActive"" = false, 
                  ""TriggeredAt"" = {0}
              WHERE ""Id"" = {1} 
                AND ""IsActive"" = true 
                AND ""TriggeredAt"" IS NULL",
            triggeredAt,
            alertId);

        return rowsAffected > 0;
    }
}

