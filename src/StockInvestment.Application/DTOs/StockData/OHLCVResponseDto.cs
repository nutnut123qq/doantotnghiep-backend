namespace StockInvestment.Application.DTOs.StockData;

/// <summary>
/// DTO for OHLCV response data
/// </summary>
public class OHLCVResponseDto
{
    public long Time { get; set; }
    public decimal Open { get; set; }
    public decimal High { get; set; }
    public decimal Low { get; set; }
    public decimal Close { get; set; }
    public long Volume { get; set; }
}
