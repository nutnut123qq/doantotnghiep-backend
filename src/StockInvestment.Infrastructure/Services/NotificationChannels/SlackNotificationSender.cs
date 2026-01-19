using System.Net.Http.Json;
using Microsoft.Extensions.Logging;
using StockInvestment.Application.Contracts.Notifications;
using StockInvestment.Application.Interfaces;
using StockInvestment.Domain.Enums;

namespace StockInvestment.Infrastructure.Services.NotificationChannels;

public class SlackNotificationSender : INotificationChannelSender
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<SlackNotificationSender> _logger;

    public NotificationChannelType ChannelType => NotificationChannelType.Slack;

    public SlackNotificationSender(HttpClient httpClient, ILogger<SlackNotificationSender> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
    }

    public async Task<bool> SendAsync(NotificationSendRequest request, CancellationToken cancellationToken)
    {
        try
        {
            var payload = new { text = request.Message };
            var response = await _httpClient.PostAsJsonAsync(request.Destination, payload, cancellationToken);
            
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("Slack API returned {StatusCode}", response.StatusCode);
                return false;
            }

            var content = await response.Content.ReadAsStringAsync(cancellationToken);
            // Trim + case-insensitive comparison
            if (content.Trim().Equals("ok", StringComparison.OrdinalIgnoreCase))
                return true;

            _logger.LogWarning("Slack returned unexpected response");
            return false;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send Slack notification");  // No webhook URL in log
            return false;
        }
    }
}
