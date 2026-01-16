namespace StockInvestment.Application.DTOs.StockData;

/// <summary>
/// DTO for Quote response data
/// </summary>
public class QuoteResponseDto
{
    public string Symbol { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Exchange { get; set; } = string.Empty;
    public decimal CurrentPrice { get; set; }
    public decimal PreviousClose { get; set; }
    public decimal Change { get; set; }
    public decimal ChangePercent { get; set; }
    public long Volume { get; set; }
    public DateTime LastUpdated { get; set; }
}
