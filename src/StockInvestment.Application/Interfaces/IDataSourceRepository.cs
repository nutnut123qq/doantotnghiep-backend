using StockInvestment.Domain.Entities;
using StockInvestment.Domain.Enums;

namespace StockInvestment.Application.Interfaces;

public interface IDataSourceRepository : IRepository<DataSource>
{
    Task<IEnumerable<DataSource>> GetByTypeAsync(DataSourceType type, CancellationToken cancellationToken = default);
    Task<DataSource?> GetByNameAsync(string name, CancellationToken cancellationToken = default);
    Task<IEnumerable<DataSource>> GetActiveSourcesAsync(CancellationToken cancellationToken = default);
}

