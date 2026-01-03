using MediatR;
using Microsoft.Extensions.Logging;
using StockInvestment.Application.Interfaces;
using StockInvestment.Domain.Entities;
using StockInvestment.Domain.Enums;
using System.Text.Json;

namespace StockInvestment.Application.Features.Alerts.CreateAlert;

public class CreateAlertHandler : IRequestHandler<CreateAlertCommand, CreateAlertResponse>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IAIService _aiService;
    private readonly ILogger<CreateAlertHandler> _logger;

    public CreateAlertHandler(
        IUnitOfWork unitOfWork,
        IAIService aiService,
        ILogger<CreateAlertHandler> logger)
    {
        _unitOfWork = unitOfWork;
        _aiService = aiService;
        _logger = logger;
    }

    public async Task<CreateAlertResponse> Handle(CreateAlertCommand request, CancellationToken cancellationToken)
    {
        Alert alert;

        // Nếu có NaturalLanguageInput, parse bằng AI
        if (!string.IsNullOrEmpty(request.NaturalLanguageInput))
        {
            var parsedAlert = await _aiService.ParseAlertAsync(request.NaturalLanguageInput);

            // Tìm ticker
            var ticker = await _unitOfWork.Repository<StockTicker>()
                .FirstOrDefaultAsync(t => t.Symbol == parsedAlert.Symbol.ToUpper(), cancellationToken);

            if (ticker == null)
            {
                throw new Exception($"Stock symbol {parsedAlert.Symbol} not found");
            }

            alert = new Alert
            {
                UserId = request.UserId,
                TickerId = ticker.Id,
                Type = ParseAlertType(parsedAlert.Type),
                Condition = JsonSerializer.Serialize(new
                {
                    Operator = parsedAlert.Operator,
                    Value = parsedAlert.Value
                }),
                Threshold = parsedAlert.Value,
                Timeframe = parsedAlert.Timeframe,
                IsActive = true,
                CreatedAt = DateTime.UtcNow
            };
        }
        else
        {
            // Tạo alert thủ công
            Guid? tickerId = null;
            if (!string.IsNullOrEmpty(request.Symbol))
            {
                var ticker = await _unitOfWork.Repository<StockTicker>()
                    .FirstOrDefaultAsync(t => t.Symbol == request.Symbol.ToUpper(), cancellationToken);
                tickerId = ticker?.Id;
            }

            alert = new Alert
            {
                UserId = request.UserId,
                TickerId = tickerId,
                Type = request.Type ?? AlertType.Price,
                Condition = request.Condition ?? "{}",
                Threshold = request.Threshold,
                Timeframe = request.Timeframe,
                IsActive = true,
                CreatedAt = DateTime.UtcNow
            };
        }

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

    private AlertType ParseAlertType(string type)
    {
        return type.ToLower() switch
        {
            "price" => AlertType.Price,
            "volume" => AlertType.Volume,
            "technical" => AlertType.TechnicalIndicator,
            "sentiment" => AlertType.Sentiment,
            _ => AlertType.Price
        };
    }
}

