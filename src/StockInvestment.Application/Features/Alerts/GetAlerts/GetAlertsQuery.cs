using MediatR;
using StockInvestment.Domain.Enums;

namespace StockInvestment.Application.Features.Alerts.GetAlerts;

public class GetAlertsQuery : IRequest<GetAlertsResponse>
{
    public Guid UserId { get; set; }
    public bool? IsActive { get; set; }
}

public class GetAlertsResponse
{
    public List<AlertDto> Alerts { get; set; } = new();
}

public class AlertDto
{
    public Guid Id { get; set; }
    public string? Symbol { get; set; }
    public AlertType Type { get; set; }
    public string Condition { get; set; } = string.Empty;
    public decimal? Threshold { get; set; }
    public string? Timeframe { get; set; }
    public bool IsActive { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? TriggeredAt { get; set; }
}

