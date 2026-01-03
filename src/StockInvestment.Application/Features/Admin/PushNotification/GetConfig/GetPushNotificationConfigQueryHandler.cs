using MediatR;
using Microsoft.Extensions.Logging;
using StockInvestment.Application.Interfaces;
using StockInvestment.Domain.Entities;

namespace StockInvestment.Application.Features.Admin.PushNotification.GetConfig;

public class GetPushNotificationConfigQueryHandler : IRequestHandler<GetPushNotificationConfigQuery, PushNotificationConfigDto?>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<GetPushNotificationConfigQueryHandler> _logger;

    public GetPushNotificationConfigQueryHandler(
        IUnitOfWork unitOfWork,
        ILogger<GetPushNotificationConfigQueryHandler> logger)
    {
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    public async Task<PushNotificationConfigDto?> Handle(GetPushNotificationConfigQuery request, CancellationToken cancellationToken)
    {
        var configs = await _unitOfWork.Repository<PushNotificationConfig>()
            .GetAllAsync(cancellationToken);

        var config = configs.FirstOrDefault();

        if (config == null)
        {
            return null;
        }

        return new PushNotificationConfigDto
        {
            Id = config.Id,
            ServiceName = config.ServiceName,
            ServerKey = config.ServerKey, // Note: In production, mask this
            AppId = config.AppId,
            Config = config.Config,
            IsEnabled = config.IsEnabled,
            CreatedAt = config.CreatedAt,
            UpdatedAt = config.UpdatedAt,
        };
    }
}

