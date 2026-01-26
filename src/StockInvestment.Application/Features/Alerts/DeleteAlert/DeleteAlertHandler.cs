using MediatR;
using Microsoft.Extensions.Logging;
using StockInvestment.Application.Interfaces;
using StockInvestment.Domain.Exceptions;

namespace StockInvestment.Application.Features.Alerts.DeleteAlert;

public class DeleteAlertHandler : IRequestHandler<DeleteAlertCommand, DeleteAlertResponse>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<DeleteAlertHandler> _logger;

    public DeleteAlertHandler(
        IUnitOfWork unitOfWork,
        ILogger<DeleteAlertHandler> logger)
    {
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    public async Task<DeleteAlertResponse> Handle(DeleteAlertCommand request, CancellationToken cancellationToken)
    {
        var alert = await _unitOfWork.Alerts.GetByIdAsync(request.AlertId, cancellationToken);

        if (alert == null)
        {
            throw new NotFoundException("Alert", request.AlertId);
        }

        // Verify ownership
        if (alert.UserId != request.UserId)
        {
            throw new UnauthorizedAccessException("You do not have permission to delete this alert");
        }

        await _unitOfWork.Alerts.DeleteAsync(alert);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Deleted alert {AlertId} for user {UserId}", alert.Id, request.UserId);

        return new DeleteAlertResponse
        {
            Success = true,
            Message = "Alert deleted successfully"
        };
    }
}
