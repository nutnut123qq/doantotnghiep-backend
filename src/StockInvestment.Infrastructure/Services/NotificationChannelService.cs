using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using StockInvestment.Application.Contracts.Notifications;
using StockInvestment.Application.Interfaces;
using StockInvestment.Domain.Entities;
using StockInvestment.Domain.Enums;

namespace StockInvestment.Infrastructure.Services;

public class NotificationChannelService : INotificationChannelService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly INotificationTemplateService _templateService;
    private readonly IEnumerable<INotificationChannelSender> _senders;
    private readonly ILogger<NotificationChannelService> _logger;

    public NotificationChannelService(
        IUnitOfWork unitOfWork,
        INotificationTemplateService templateService,
        IEnumerable<INotificationChannelSender> senders,
        ILogger<NotificationChannelService> logger)
    {
        _unitOfWork = unitOfWork;
        _templateService = templateService;
        _senders = senders;
        _logger = logger;
    }

    public async Task<NotificationChannelConfigDto?> GetUserConfigAsync(Guid userId, CancellationToken cancellationToken)
    {
        var config = await _unitOfWork.Repository<NotificationChannelConfig>()
            .FirstOrDefaultAsync(c => c.UserId == userId, cancellationToken);

        if (config == null)
            return null;

        // Không trả webhook thật, chỉ trả flag
        return new NotificationChannelConfigDto
        {
            HasSlackWebhook = !string.IsNullOrEmpty(config.SlackWebhookUrl),
            EnabledSlack = config.EnabledSlack,
            TelegramChatId = config.TelegramChatId,
            EnabledTelegram = config.EnabledTelegram
        };
    }

    public async Task<NotificationChannelConfigDto> SaveConfigAsync(
        Guid userId,
        UpdateNotificationChannelRequest request,
        CancellationToken cancellationToken)
    {
        // Load existing config FIRST
        var config = await _unitOfWork.Repository<NotificationChannelConfig>()
            .FirstOrDefaultAsync(c => c.UserId == userId, cancellationToken);

        // Validate theo "effective value" (request mới hoặc DB hiện tại)
        var effectiveSlackWebhook =
            !string.IsNullOrWhiteSpace(request.SlackWebhookUrl)
                ? request.SlackWebhookUrl
                : config?.SlackWebhookUrl;

        var effectiveTelegramChatId =
            !string.IsNullOrWhiteSpace(request.TelegramChatId)
                ? request.TelegramChatId
                : config?.TelegramChatId;

        // Validate Slack
        if (request.EnabledSlack)
        {
            if (string.IsNullOrWhiteSpace(effectiveSlackWebhook))
                throw new InvalidOperationException("Slack webhook URL required when Slack is enabled");

            if (!effectiveSlackWebhook.StartsWith("https://hooks.slack.com/"))
                throw new InvalidOperationException("Invalid Slack webhook URL format");
        }

        // Validate Telegram
        if (request.EnabledTelegram && string.IsNullOrWhiteSpace(effectiveTelegramChatId))
            throw new InvalidOperationException("Telegram chat ID required when Telegram is enabled");

        if (config == null)
        {
            // Create new
            config = new NotificationChannelConfig
            {
                UserId = userId,
                SlackWebhookUrl = request.SlackWebhookUrl,
                EnabledSlack = request.EnabledSlack,
                TelegramChatId = request.TelegramChatId,
                EnabledTelegram = request.EnabledTelegram,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };
            await _unitOfWork.Repository<NotificationChannelConfig>().AddAsync(config, cancellationToken);
        }
        else
        {
            // Update: chỉ update nếu có giá trị mới
            if (!string.IsNullOrWhiteSpace(request.SlackWebhookUrl))
                config.SlackWebhookUrl = request.SlackWebhookUrl;

            config.EnabledSlack = request.EnabledSlack;

            if (!string.IsNullOrWhiteSpace(request.TelegramChatId))
                config.TelegramChatId = request.TelegramChatId;

            config.EnabledTelegram = request.EnabledTelegram;
            config.UpdatedAt = DateTime.UtcNow;
        }

        try
        {
            await _unitOfWork.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            throw new InvalidOperationException("Config was updated by another request");
        }

        return await GetUserConfigAsync(userId, cancellationToken) ?? new NotificationChannelConfigDto();
    }

    public async Task SendAlertNotificationAsync(AlertTriggeredContext context, CancellationToken cancellationToken)
    {
        var config = await _unitOfWork.Repository<NotificationChannelConfig>()
            .FirstOrDefaultAsync(c => c.UserId == context.UserId, cancellationToken);

        if (config == null)
        {
            _logger.LogDebug("No notification config for user {UserId}", context.UserId);
            return;
        }

        // Load template
        var eventType = context.Alert.Type == AlertType.Price
            ? NotificationEventType.PriceAlert
            : NotificationEventType.VolumeAlert;

        var allTemplates = await _unitOfWork.Repository<NotificationTemplate>()
            .GetAllAsync(cancellationToken);

        var template = allTemplates
            .Where(t => t.EventType == eventType && t.IsActive)
            .FirstOrDefault();
        if (template == null)
        {
            _logger.LogWarning("No template found for {EventType}", eventType);
            return;
        }

        // Build variables
        var variables = new Dictionary<string, string>
        {
            { "Symbol", context.Alert.Ticker?.Symbol ?? "Unknown" },
            { "AlertType", context.Alert.Type.ToString() },
            { "Operator", context.Operator },  // Use directly
            { "Threshold", context.Alert.Threshold?.ToString("N0") ?? "0" },
            { "CurrentValue", context.CurrentValue.ToString("N0") },
            { "Time", context.TriggeredAt.ToString("yyyy-MM-dd HH:mm:ss") },
            { "AiExplanation", context.AiExplanation ?? "AI explanation unavailable" }
        };

        var message = await _templateService.RenderTemplateAsync(template.Id, variables, cancellationToken);

        // Send to enabled channels
        if (config.EnabledSlack && !string.IsNullOrEmpty(config.SlackWebhookUrl))
        {
            var slackRequest = new NotificationSendRequest
            {
                ChannelType = NotificationChannelType.Slack,
                Destination = config.SlackWebhookUrl,
                Message = message
            };

            var slackSender = _senders.FirstOrDefault(s => s.ChannelType == NotificationChannelType.Slack);
            if (slackSender != null)
            {
                var success = await slackSender.SendAsync(slackRequest, cancellationToken);
                _logger.LogInformation("Slack notification {Result} for alert {AlertId}",
                    success ? "sent" : "failed", context.Alert.Id);
            }
        }

        if (config.EnabledTelegram && !string.IsNullOrEmpty(config.TelegramChatId))
        {
            var telegramRequest = new NotificationSendRequest
            {
                ChannelType = NotificationChannelType.Telegram,
                Destination = config.TelegramChatId,
                Message = message
            };

            var telegramSender = _senders.FirstOrDefault(s => s.ChannelType == NotificationChannelType.Telegram);
            if (telegramSender != null)
            {
                var success = await telegramSender.SendAsync(telegramRequest, cancellationToken);
                _logger.LogInformation("Telegram notification {Result} for alert {AlertId}",
                    success ? "sent" : "failed", context.Alert.Id);
            }
        }
    }

    public async Task<bool> TestChannelAsync(Guid userId, NotificationChannelType channelType, CancellationToken cancellationToken)
    {
        // Validate config exists và lấy destination từ DB
        var config = await _unitOfWork.Repository<NotificationChannelConfig>()
            .FirstOrDefaultAsync(c => c.UserId == userId, cancellationToken);

        if (config == null)
            throw new InvalidOperationException("Please configure notification channels first");

        var testMessage = "🔔 Test notification from Stock Investment Platform";
        var request = new NotificationSendRequest { Message = testMessage };

        if (channelType == NotificationChannelType.Slack)
        {
            if (!config.EnabledSlack || string.IsNullOrEmpty(config.SlackWebhookUrl))
                throw new InvalidOperationException("Slack channel not configured");

            request.ChannelType = NotificationChannelType.Slack;
            request.Destination = config.SlackWebhookUrl;
        }
        else if (channelType == NotificationChannelType.Telegram)
        {
            if (!config.EnabledTelegram || string.IsNullOrEmpty(config.TelegramChatId))
                throw new InvalidOperationException("Telegram channel not configured");

            request.ChannelType = NotificationChannelType.Telegram;
            request.Destination = config.TelegramChatId;
        }

        var sender = _senders.FirstOrDefault(s => s.ChannelType == channelType);
        if (sender == null)
            return false;

        return await sender.SendAsync(request, cancellationToken);
    }
}
