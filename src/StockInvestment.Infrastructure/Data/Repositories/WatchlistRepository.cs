using Microsoft.EntityFrameworkCore;
using StockInvestment.Application.Interfaces;
using StockInvestment.Domain.Entities;

namespace StockInvestment.Infrastructure.Data.Repositories;

public class WatchlistRepository : Repository<Watchlist>, IWatchlistRepository
{
    public WatchlistRepository(ApplicationDbContext context) : base(context)
    {
    }

    public async Task<IEnumerable<Watchlist>> GetByUserIdWithTickersAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        return await _dbSet
            .Include(w => w.Tickers)
            .Where(w => w.UserId == userId)
            .OrderByDescending(w => w.CreatedAt)
            .ToListAsync(cancellationToken);
    }

    public async Task<Watchlist?> GetByIdWithTickersAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await _dbSet
            .Include(w => w.Tickers)
            .FirstOrDefaultAsync(w => w.Id == id, cancellationToken);
    }
}

