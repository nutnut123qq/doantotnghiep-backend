using MediatR;
using Microsoft.Extensions.Logging;
using StockInvestment.Application.Interfaces;
using StockInvestment.Domain.Exceptions;

namespace StockInvestment.Application.Features.Alerts.ToggleAlert;

public class ToggleAlertHandler : IRequestHandler<ToggleAlertCommand, ToggleAlertResponse>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<ToggleAlertHandler> _logger;

    public ToggleAlertHandler(
        IUnitOfWork unitOfWork,
        ILogger<ToggleAlertHandler> logger)
    {
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    public async Task<ToggleAlertResponse> Handle(ToggleAlertCommand request, CancellationToken cancellationToken)
    {
        var alert = await _unitOfWork.Alerts.GetByIdAsync(request.AlertId, cancellationToken);

        if (alert == null)
        {
            throw new NotFoundException("Alert", request.AlertId);
        }

        // Verify ownership
        if (alert.UserId != request.UserId)
        {
            throw new UnauthorizedAccessException("You do not have permission to toggle this alert");
        }

        alert.IsActive = request.IsActive;
        await _unitOfWork.Alerts.UpdateAsync(alert);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Toggled alert {AlertId} to {IsActive} for user {UserId}", 
            alert.Id, request.IsActive, request.UserId);

        return new ToggleAlertResponse
        {
            Id = alert.Id,
            IsActive = alert.IsActive
        };
    }
}
