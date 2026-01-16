namespace StockInvestment.Application.Interfaces;

public interface IPortfolioService
{
    Task<IEnumerable<PortfolioHoldingDto>> GetHoldingsAsync(Guid userId);
    Task<PortfolioSummaryDto> GetSummaryAsync(Guid userId);
    Task<PortfolioHoldingDto> AddHoldingAsync(Guid userId, AddHoldingRequest request);
    Task<PortfolioHoldingDto> UpdateHoldingAsync(Guid userId, Guid holdingId, UpdateHoldingRequest request);
    Task DeleteHoldingAsync(Guid userId, Guid holdingId);
}

public class PortfolioHoldingDto
{
    public string Id { get; set; } = string.Empty;
    public string Symbol { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public decimal Shares { get; set; }
    public decimal AvgPrice { get; set; }
    public decimal CurrentPrice { get; set; }
    public decimal Value { get; set; }
    public decimal GainLoss { get; set; }
    public decimal GainLossPercentage { get; set; }
}

public class PortfolioSummaryDto
{
    public decimal TotalValue { get; set; }
    public decimal TotalCost { get; set; }
    public decimal TotalGainLoss { get; set; }
    public decimal TotalGainLossPercentage { get; set; }
    public int HoldingsCount { get; set; }
}

public class AddHoldingRequest
{
    public string Symbol { get; set; } = string.Empty;
    public decimal Shares { get; set; }
    public decimal AvgPrice { get; set; }
}

public class UpdateHoldingRequest
{
    public decimal Shares { get; set; }
    public decimal AvgPrice { get; set; }
}
