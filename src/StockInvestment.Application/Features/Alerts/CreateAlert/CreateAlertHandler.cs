using MediatR;
using Microsoft.Extensions.Logging;
using StockInvestment.Application.Interfaces;
using StockInvestment.Domain.Entities;
using StockInvestment.Domain.Enums;

namespace StockInvestment.Application.Features.Alerts.CreateAlert;

public class CreateAlertHandler : IRequestHandler<CreateAlertCommand, CreateAlertResponse>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<CreateAlertHandler> _logger;

    public CreateAlertHandler(
        IUnitOfWork unitOfWork,
        ILogger<CreateAlertHandler> logger)
    {
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    public async Task<CreateAlertResponse> Handle(CreateAlertCommand request, CancellationToken cancellationToken)
    {
        Guid? tickerId = null;
        if (!string.IsNullOrEmpty(request.Symbol))
        {
            var ticker = await _unitOfWork.Repository<StockTicker>()
                .FirstOrDefaultAsync(t => t.Symbol == request.Symbol.ToUpper(), cancellationToken);
            tickerId = ticker?.Id;
        }

        var alertType = request.Type ?? AlertType.Price;
        if (!Enum.IsDefined(typeof(AlertType), alertType))
        {
            throw new ArgumentException($"Alert type '{alertType}' is not supported. Only Price and Volume alerts are available.", nameof(request.Type));
        }

        var alert = new Alert
        {
            UserId = request.UserId,
            TickerId = tickerId,
            Type = alertType,
            Condition = request.Condition ?? "{}",
            Threshold = request.Threshold,
            Timeframe = request.Timeframe,
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        };

        await _unitOfWork.Alerts.AddAsync(alert, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Created alert {AlertId} for user {UserId}", alert.Id, request.UserId);

        return new CreateAlertResponse
        {
            Id = alert.Id,
            Symbol = alert.Ticker?.Symbol ?? "",
            Type = alert.Type,
            Condition = alert.Condition,
            Threshold = alert.Threshold,
            IsActive = alert.IsActive,
            CreatedAt = alert.CreatedAt
        };
    }
}
