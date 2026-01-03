using StockInvestment.Domain.Entities;

namespace StockInvestment.Application.Interfaces;

public interface IWatchlistRepository : IRepository<Watchlist>
{
    Task<IEnumerable<Watchlist>> GetByUserIdWithTickersAsync(Guid userId, CancellationToken cancellationToken = default);
    Task<Watchlist?> GetByIdWithTickersAsync(Guid id, CancellationToken cancellationToken = default);
}

