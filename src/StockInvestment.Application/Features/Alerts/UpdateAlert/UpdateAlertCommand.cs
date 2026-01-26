using MediatR;
using StockInvestment.Domain.Enums;

namespace StockInvestment.Application.Features.Alerts.UpdateAlert;

public class UpdateAlertCommand : IRequest<UpdateAlertResponse>
{
    public Guid AlertId { get; set; }
    public Guid UserId { get; set; }
    public string? Symbol { get; set; }
    public AlertType? Type { get; set; }
    public string? Condition { get; set; }
    public decimal? Threshold { get; set; }
    public string? Timeframe { get; set; }
}

public class UpdateAlertResponse
{
    public Guid Id { get; set; }
    public string Symbol { get; set; } = string.Empty;
    public AlertType Type { get; set; }
    public string Condition { get; set; } = string.Empty;
    public decimal? Threshold { get; set; }
    public string? Timeframe { get; set; }
    public bool IsActive { get; set; }
    public DateTime CreatedAt { get; set; }
}
