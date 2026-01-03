using StockInvestment.Domain.Entities;

namespace StockInvestment.Application.Interfaces;

public interface IUserPreferenceRepository : IRepository<UserPreference>
{
    /// <summary>
    /// Get all preferences for a specific user
    /// </summary>
    Task<IEnumerable<UserPreference>> GetByUserIdAsync(Guid userId);
    
    /// <summary>
    /// Get a specific preference by user ID and preference key
    /// </summary>
    Task<UserPreference?> GetByUserAndKeyAsync(Guid userId, string preferenceKey);
    
    /// <summary>
    /// Delete a specific preference by user ID and preference key
    /// </summary>
    Task DeleteByUserAndKeyAsync(Guid userId, string preferenceKey);
}
