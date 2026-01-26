using MediatR;

namespace StockInvestment.Application.Features.Alerts.ToggleAlert;

public class ToggleAlertCommand : IRequest<ToggleAlertResponse>
{
    public Guid AlertId { get; set; }
    public Guid UserId { get; set; }
    public bool IsActive { get; set; }
}

public class ToggleAlertResponse
{
    public Guid Id { get; set; }
    public bool IsActive { get; set; }
}
