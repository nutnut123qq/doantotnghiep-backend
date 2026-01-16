using StockInvestment.Domain.Entities;

namespace StockInvestment.Application.Interfaces;

/// <summary>
/// Repository interface for Portfolio
/// </summary>
public interface IPortfolioRepository : IRepository<Portfolio>
{
    /// <summary>
    /// Get all portfolios for a user
    /// </summary>
    Task<IEnumerable<Portfolio>> GetByUserIdAsync(Guid userId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Find portfolio by user ID and symbol
    /// </summary>
    Task<Portfolio?> FindByUserAndSymbolAsync(Guid userId, string symbol, CancellationToken cancellationToken = default);

    /// <summary>
    /// Get portfolio by ID and user ID
    /// </summary>
    Task<Portfolio?> GetByIdAndUserIdAsync(Guid id, Guid userId, CancellationToken cancellationToken = default);
}
