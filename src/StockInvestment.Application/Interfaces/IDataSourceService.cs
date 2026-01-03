using StockInvestment.Domain.Entities;
using StockInvestment.Domain.Enums;

namespace StockInvestment.Application.Interfaces;

public interface IDataSourceService
{
    Task<IEnumerable<DataSource>> GetAllAsync(CancellationToken cancellationToken = default);
    Task<DataSource?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<IEnumerable<DataSource>> GetByTypeAsync(DataSourceType type, CancellationToken cancellationToken = default);
    Task<DataSource> CreateAsync(DataSource dataSource, CancellationToken cancellationToken = default);
    Task<DataSource> UpdateAsync(DataSource dataSource, CancellationToken cancellationToken = default);
    Task DeleteAsync(Guid id, CancellationToken cancellationToken = default);
    Task<bool> TestConnectionAsync(Guid id, CancellationToken cancellationToken = default);
}

