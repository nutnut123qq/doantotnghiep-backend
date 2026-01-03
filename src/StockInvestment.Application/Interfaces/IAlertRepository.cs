using StockInvestment.Domain.Entities;

namespace StockInvestment.Application.Interfaces;

public interface IAlertRepository : IRepository<Alert>
{
    Task<IEnumerable<Alert>> GetByUserIdWithTickerAsync(Guid userId, bool? isActive = null, CancellationToken cancellationToken = default);
    Task<IEnumerable<Alert>> GetActiveAlertsWithTickerAndUserAsync(CancellationToken cancellationToken = default);
}

