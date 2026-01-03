using MediatR;
using StockInvestment.Domain.Enums;

namespace StockInvestment.Application.Features.Alerts.CreateAlert;

public class CreateAlertCommand : IRequest<CreateAlertResponse>
{
    public Guid UserId { get; set; }
    public string? Symbol { get; set; }
    public string? NaturalLanguageInput { get; set; } // "Notify me when VIC reaches 100,000"
    public AlertType? Type { get; set; }
    public string? Condition { get; set; }
    public decimal? Threshold { get; set; }
    public string? Timeframe { get; set; }
}

public class CreateAlertResponse
{
    public Guid Id { get; set; }
    public string Symbol { get; set; } = string.Empty;
    public AlertType Type { get; set; }
    public string Condition { get; set; } = string.Empty;
    public decimal? Threshold { get; set; }
    public bool IsActive { get; set; }
    public DateTime CreatedAt { get; set; }
}

