using MediatR;
using Microsoft.Extensions.Logging;
using StockInvestment.Application.Interfaces;

namespace StockInvestment.Application.Features.Admin.PushNotification.Test;

public class TestPushNotificationCommandHandler : IRequestHandler<TestPushNotificationCommand, TestPushNotificationResponse>
{
    private readonly IPushNotificationService _pushNotificationService;
    private readonly ILogger<TestPushNotificationCommandHandler> _logger;

    public TestPushNotificationCommandHandler(
        IPushNotificationService pushNotificationService,
        ILogger<TestPushNotificationCommandHandler> logger)
    {
        _pushNotificationService = pushNotificationService;
        _logger = logger;
    }

    public async Task<TestPushNotificationResponse> Handle(TestPushNotificationCommand request, CancellationToken cancellationToken)
    {
        try
        {
            var success = await _pushNotificationService.TestPushNotificationAsync(
                request.DeviceToken,
                request.Title,
                request.Body,
                cancellationToken
            );

            return new TestPushNotificationResponse
            {
                Success = success,
                ErrorMessage = success ? null : "Failed to send test notification",
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error testing push notification");
            return new TestPushNotificationResponse
            {
                Success = false,
                ErrorMessage = ex.Message,
            };
        }
    }
}

