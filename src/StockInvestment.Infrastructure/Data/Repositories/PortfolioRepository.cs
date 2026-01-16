using Microsoft.EntityFrameworkCore;
using StockInvestment.Application.Interfaces;
using StockInvestment.Domain.Entities;
using StockInvestment.Infrastructure.Data;

namespace StockInvestment.Infrastructure.Data.Repositories;

/// <summary>
/// Repository implementation for Portfolio
/// </summary>
public class PortfolioRepository : Repository<Portfolio>, IPortfolioRepository
{
    public PortfolioRepository(ApplicationDbContext context) : base(context)
    {
    }

    public async Task<IEnumerable<Portfolio>> GetByUserIdAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        return await _dbSet
            .Where(p => p.UserId == userId)
            .ToListAsync(cancellationToken);
    }

    public async Task<Portfolio?> FindByUserAndSymbolAsync(Guid userId, string symbol, CancellationToken cancellationToken = default)
    {
        return await _dbSet
            .FirstOrDefaultAsync(p => p.UserId == userId && p.Symbol == symbol, cancellationToken);
    }

    public async Task<Portfolio?> GetByIdAndUserIdAsync(Guid id, Guid userId, CancellationToken cancellationToken = default)
    {
        return await _dbSet
            .FirstOrDefaultAsync(p => p.Id == id && p.UserId == userId, cancellationToken);
    }
}
