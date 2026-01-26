using MediatR;

namespace StockInvestment.Application.Features.Alerts.DeleteAlert;

public class DeleteAlertCommand : IRequest<DeleteAlertResponse>
{
    public Guid AlertId { get; set; }
    public Guid UserId { get; set; }
}

public class DeleteAlertResponse
{
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
}
