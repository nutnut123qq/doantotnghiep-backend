namespace StockInvestment.Application.DTOs.Forecast;

/// <summary>
/// Response DTO for forecast endpoint with multiple time horizons
/// </summary>
public class ForecastResponseDto
{
    public string Symbol { get; set; } = string.Empty;
    public ForecastTimeHorizonsDto Forecasts { get; set; } = new();
}

/// <summary>
/// DTO for forecast time horizons
/// </summary>
public class ForecastTimeHorizonsDto
{
    public object? ShortTerm { get; set; }
    public object? MediumTerm { get; set; }
    public object? LongTerm { get; set; }
}
