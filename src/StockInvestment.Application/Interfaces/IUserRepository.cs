using StockInvestment.Domain.Entities;
using StockInvestment.Domain.ValueObjects;

namespace StockInvestment.Application.Interfaces;

/// <summary>
/// User repository interface extending generic repository
/// </summary>
public interface IUserRepository : IRepository<User>
{
    /// <summary>
    /// Get user by email
    /// </summary>
    Task<User?> GetByEmailAsync(Email email, CancellationToken cancellationToken = default);
}

