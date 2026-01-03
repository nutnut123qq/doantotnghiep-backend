using MediatR;
using Microsoft.Extensions.Logging;
using StockInvestment.Application.Interfaces;

namespace StockInvestment.Application.Features.Alerts.GetAlerts;

public class GetAlertsHandler : IRequestHandler<GetAlertsQuery, GetAlertsResponse>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<GetAlertsHandler> _logger;

    public GetAlertsHandler(
        IUnitOfWork unitOfWork,
        ILogger<GetAlertsHandler> logger)
    {
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    public async Task<GetAlertsResponse> Handle(GetAlertsQuery request, CancellationToken cancellationToken)
    {
        var alerts = await _unitOfWork.Alerts.GetByUserIdWithTickerAsync(
            request.UserId,
            request.IsActive,
            cancellationToken);

        var response = new GetAlertsResponse
        {
            Alerts = alerts.Select(a => new AlertDto
            {
                Id = a.Id,
                Symbol = a.Ticker?.Symbol,
                Type = a.Type,
                Condition = a.Condition,
                Threshold = a.Threshold,
                Timeframe = a.Timeframe,
                IsActive = a.IsActive,
                CreatedAt = a.CreatedAt,
                TriggeredAt = a.TriggeredAt
            }).ToList()
        };

        return response;
    }
}

