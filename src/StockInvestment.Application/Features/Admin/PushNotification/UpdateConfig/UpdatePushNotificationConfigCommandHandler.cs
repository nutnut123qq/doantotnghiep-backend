using MediatR;
using Microsoft.Extensions.Logging;
using StockInvestment.Application.Interfaces;
using StockInvestment.Domain.Entities;

namespace StockInvestment.Application.Features.Admin.PushNotification.UpdateConfig;

public class UpdatePushNotificationConfigCommandHandler : IRequestHandler<UpdatePushNotificationConfigCommand, PushNotificationConfigDto>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<UpdatePushNotificationConfigCommandHandler> _logger;

    public UpdatePushNotificationConfigCommandHandler(
        IUnitOfWork unitOfWork,
        ILogger<UpdatePushNotificationConfigCommandHandler> logger)
    {
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    public async Task<PushNotificationConfigDto> Handle(UpdatePushNotificationConfigCommand request, CancellationToken cancellationToken)
    {
        var existing = request.Id.HasValue
            ? await _unitOfWork.Repository<PushNotificationConfig>()
                .GetByIdAsync(request.Id.Value, cancellationToken)
            : null;

        var config = existing ?? new PushNotificationConfig
        {
            Id = request.Id ?? Guid.NewGuid(),
            CreatedAt = DateTime.UtcNow,
        };

        config.ServiceName = request.ServiceName;
        config.ServerKey = request.ServerKey;
        config.AppId = request.AppId;
        config.Config = request.Config;
        config.IsEnabled = request.IsEnabled;
        config.UpdatedAt = DateTime.UtcNow;

        if (existing == null)
        {
            await _unitOfWork.Repository<PushNotificationConfig>().AddAsync(config, cancellationToken);
        }
        else
        {
            await _unitOfWork.Repository<PushNotificationConfig>().UpdateAsync(config, cancellationToken);
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Updated push notification config: {ServiceName} ({Id})", config.ServiceName, config.Id);

        return new PushNotificationConfigDto
        {
            Id = config.Id,
            ServiceName = config.ServiceName,
            ServerKey = config.ServerKey,
            AppId = config.AppId,
            Config = config.Config,
            IsEnabled = config.IsEnabled,
            CreatedAt = config.CreatedAt,
            UpdatedAt = config.UpdatedAt,
        };
    }
}

