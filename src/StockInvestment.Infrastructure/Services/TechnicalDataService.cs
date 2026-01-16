using Microsoft.Extensions.Logging;
using StockInvestment.Application.Interfaces;

namespace StockInvestment.Infrastructure.Services;

/// <summary>
/// Service for preparing technical indicator data for AI services
/// </summary>
public class TechnicalDataService : ITechnicalDataService
{
    private readonly ITechnicalIndicatorService _technicalIndicatorService;
    private readonly ILogger<TechnicalDataService> _logger;

    public TechnicalDataService(
        ITechnicalIndicatorService technicalIndicatorService,
        ILogger<TechnicalDataService> logger)
    {
        _technicalIndicatorService = technicalIndicatorService;
        _logger = logger;
    }

    public async Task<Dictionary<string, string>> PrepareTechnicalDataAsync(string symbol, CancellationToken cancellationToken = default)
    {
        var technicalData = new Dictionary<string, string>();

        try
        {
            var indicators = await _technicalIndicatorService.CalculateAllIndicatorsAsync(symbol);
            
            // Optimize: Single enumeration using dictionary
            var indicatorsByType = indicators.ToDictionary(
                i => i.IndicatorType, 
                StringComparer.OrdinalIgnoreCase);
            
            var ma20 = indicatorsByType.GetValueOrDefault("MA20");
            var ma50 = indicatorsByType.GetValueOrDefault("MA50");
            var rsi = indicatorsByType.GetValueOrDefault("RSI");
            var macd = indicatorsByType.GetValueOrDefault("MACD");

            if (ma20 != null && ma20.Value.HasValue)
                technicalData["ma"] = $"MA20: {ma20.Value.Value:F2} - {ma20.TrendAssessment ?? "N/A"}";
            if (rsi != null && rsi.Value.HasValue)
                technicalData["rsi"] = $"{rsi.Value.Value:F2}";
            if (macd != null && macd.Value.HasValue)
                technicalData["macd"] = $"MACD: {macd.Value.Value:F2} - {macd.TrendAssessment ?? "N/A"}";
            
            // Optimize: Use dictionary values instead of re-enumerating
            var trendAssessment = indicatorsByType.Values
                .Select(i => i.TrendAssessment)
                .Where(t => !string.IsNullOrEmpty(t))
                .Distinct()
                .ToList();
            technicalData["trend"] = trendAssessment.Any() 
                ? string.Join(", ", trendAssessment)
                : "Chưa có dữ liệu";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error preparing technical data for {Symbol}", symbol);
            throw new Domain.Exceptions.ExternalServiceException(
                "TechnicalIndicatorService",
                $"Failed to prepare technical data for {symbol}",
                ex);
        }

        return technicalData;
    }
}
