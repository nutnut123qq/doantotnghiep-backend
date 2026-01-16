namespace StockInvestment.Application.DTOs.Forecast;

/// <summary>
/// Request DTO for batch forecast generation
/// </summary>
public class BatchForecastRequest
{
    public List<string> Symbols { get; set; } = new();
    public string TimeHorizon { get; set; } = "short";
}
