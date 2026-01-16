namespace StockInvestment.Application.DTOs.Forecast;

/// <summary>
/// Response DTO for batch forecast generation
/// </summary>
public class BatchForecastResponseDto
{
    public List<ForecastItemDto> Forecasts { get; set; } = new();
}

/// <summary>
/// DTO for individual forecast item in batch
/// </summary>
public class ForecastItemDto
{
    public string Symbol { get; set; } = string.Empty;
    public string? Trend { get; set; }
    public string? Confidence { get; set; }
    public string? Recommendation { get; set; }
    public string? Error { get; set; }
}
