using MediatR;
using Microsoft.Extensions.Logging;
using StockInvestment.Application.Interfaces;
using StockInvestment.Domain.Exceptions;
using StockInvestment.Domain.Entities;
using StockTicker = StockInvestment.Domain.Entities.StockTicker;

namespace StockInvestment.Application.Features.Alerts.UpdateAlert;

public class UpdateAlertHandler : IRequestHandler<UpdateAlertCommand, UpdateAlertResponse>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<UpdateAlertHandler> _logger;

    public UpdateAlertHandler(
        IUnitOfWork unitOfWork,
        ILogger<UpdateAlertHandler> logger)
    {
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    public async Task<UpdateAlertResponse> Handle(UpdateAlertCommand request, CancellationToken cancellationToken)
    {
        var alert = await _unitOfWork.Alerts.GetByIdAsync(request.AlertId, cancellationToken);

        if (alert == null)
        {
            throw new NotFoundException("Alert", request.AlertId);
        }

        // Verify ownership
        if (alert.UserId != request.UserId)
        {
            throw new UnauthorizedAccessException("You do not have permission to update this alert");
        }

        // Update fields if provided
        if (!string.IsNullOrEmpty(request.Symbol))
        {
            var ticker = await _unitOfWork.Repository<StockTicker>()
                .FirstOrDefaultAsync(t => t.Symbol == request.Symbol.ToUpper(), cancellationToken);
            
            if (ticker == null)
            {
                throw new NotFoundException("StockTicker", request.Symbol);
            }
            
            alert.TickerId = ticker.Id;
        }

        if (request.Type.HasValue)
        {
            alert.Type = request.Type.Value;
        }

        if (request.Condition != null)
        {
            alert.Condition = request.Condition;
        }

        if (request.Threshold.HasValue)
        {
            alert.Threshold = request.Threshold.Value;
        }

        if (request.Timeframe != null)
        {
            alert.Timeframe = request.Timeframe;
        }

        await _unitOfWork.Alerts.UpdateAsync(alert);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Updated alert {AlertId} for user {UserId}", alert.Id, request.UserId);

        return new UpdateAlertResponse
        {
            Id = alert.Id,
            Symbol = alert.Ticker?.Symbol ?? "",
            Type = alert.Type,
            Condition = alert.Condition,
            Threshold = alert.Threshold,
            Timeframe = alert.Timeframe,
            IsActive = alert.IsActive,
            CreatedAt = alert.CreatedAt
        };
    }
}
