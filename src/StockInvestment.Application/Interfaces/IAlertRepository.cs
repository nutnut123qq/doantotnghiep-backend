using StockInvestment.Domain.Entities;

namespace StockInvestment.Application.Interfaces;

public interface IAlertRepository : IRepository<Alert>
{
    Task<IEnumerable<Alert>> GetByUserIdWithTickerAsync(Guid userId, bool? isActive = null, CancellationToken cancellationToken = default);
    Task<IEnumerable<Alert>> GetActiveAlertsWithTickerAndUserAsync(CancellationToken cancellationToken = default);
    
    /// <summary>
    /// P0-3: Atomically mark an alert as triggered to prevent duplicate notifications in multi-instance scenarios
    /// Returns true if the update succeeded (alert was active and not already triggered), false otherwise
    /// </summary>
    Task<bool> TryMarkAsTriggeredAsync(Guid alertId, DateTime triggeredAt, CancellationToken cancellationToken = default);
}

