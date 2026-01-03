using Microsoft.EntityFrameworkCore;
using StockInvestment.Application.Interfaces;
using StockInvestment.Domain.Entities;
using StockInvestment.Domain.Enums;

namespace StockInvestment.Infrastructure.Data.Repositories;

public class DataSourceRepository : Repository<DataSource>, IDataSourceRepository
{
    public DataSourceRepository(ApplicationDbContext context) : base(context)
    {
    }

    public async Task<IEnumerable<DataSource>> GetByTypeAsync(DataSourceType type, CancellationToken cancellationToken = default)
    {
        return await _dbSet
            .Where(ds => ds.Type == type)
            .OrderBy(ds => ds.Name)
            .ToListAsync(cancellationToken);
    }

    public async Task<DataSource?> GetByNameAsync(string name, CancellationToken cancellationToken = default)
    {
        return await _dbSet
            .FirstOrDefaultAsync(ds => ds.Name == name, cancellationToken);
    }

    public async Task<IEnumerable<DataSource>> GetActiveSourcesAsync(CancellationToken cancellationToken = default)
    {
        return await _dbSet
            .Where(ds => ds.IsActive)
            .OrderBy(ds => ds.Type)
            .ThenBy(ds => ds.Name)
            .ToListAsync(cancellationToken);
    }
}

