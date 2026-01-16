namespace StockInvestment.Application.Interfaces;

/// <summary>
/// Service for preparing technical indicator data for AI services
/// </summary>
public interface ITechnicalDataService
{
    /// <summary>
    /// Prepare technical data dictionary from indicators for a symbol
    /// </summary>
    Task<Dictionary<string, string>> PrepareTechnicalDataAsync(string symbol, CancellationToken cancellationToken = default);
}
