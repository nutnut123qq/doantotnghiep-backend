using StockInvestment.Domain.Entities;

namespace StockInvestment.Application.Interfaces;

/// <summary>
/// Chart Settings repository interface
/// </summary>
public interface IChartSettingsRepository : IRepository<ChartSettings>
{
    /// <summary>
    /// Get chart settings by user ID and symbol
    /// </summary>
    Task<ChartSettings?> GetByUserAndSymbolAsync(Guid userId, string symbol, CancellationToken cancellationToken = default);

    /// <summary>
    /// Get all chart settings for a user
    /// </summary>
    Task<IEnumerable<ChartSettings>> GetByUserIdAsync(Guid userId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Save or update chart settings (upsert)
    /// </summary>
    Task<ChartSettings> SaveSettingsAsync(ChartSettings settings, CancellationToken cancellationToken = default);
}
