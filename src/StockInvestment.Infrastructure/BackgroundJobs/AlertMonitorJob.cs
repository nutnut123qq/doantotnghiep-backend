using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using StockInvestment.Application.Contracts.Notifications;
using StockInvestment.Application.Interfaces;
using StockInvestment.Domain.Enums;
using StockInvestment.Infrastructure.Hubs;
using System.Text.Json;

namespace StockInvestment.Infrastructure.BackgroundJobs;

/// <summary>
/// Background job to monitor and trigger alerts
/// </summary>
public class AlertMonitorJob : BackgroundService
{
    private readonly ILogger<AlertMonitorJob> _logger;
    private readonly IServiceProvider _serviceProvider;
    private readonly TimeSpan _checkInterval = TimeSpan.FromMinutes(1);

    public AlertMonitorJob(
        ILogger<AlertMonitorJob> logger,
        IServiceProvider serviceProvider)
    {
        _logger = logger;
        _serviceProvider = serviceProvider;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Alert Monitor Job started");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await CheckAlertsAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error checking alerts");
            }

            await Task.Delay(_checkInterval, stoppingToken);
        }

        _logger.LogInformation("Alert Monitor Job stopped");
    }

    private async Task CheckAlertsAsync(CancellationToken cancellationToken)
    {
        using var scope = _serviceProvider.CreateScope();
        var unitOfWork = scope.ServiceProvider.GetRequiredService<StockInvestment.Application.Interfaces.IUnitOfWork>();

        try
        {
            var activeAlerts = await unitOfWork.Alerts.GetActiveAlertsWithTickerAndUserAsync(cancellationToken);

            _logger.LogInformation("Checking {Count} active alerts", activeAlerts.Count());

            foreach (var alert in activeAlerts)
            {
                if (alert.Ticker == null) continue;

                // Anti-spam: Check cooldown period
                if (alert.TriggeredAt.HasValue &&
                    DateTime.UtcNow - alert.TriggeredAt.Value < TimeSpan.FromMinutes(5))
                {
                    _logger.LogDebug("Alert {AlertId} in cooldown period, skipping", alert.Id);
                    continue;
                }

                bool shouldTrigger = false;
                decimal currentValue = 0;  // Runtime snapshot

                switch (alert.Type)
                {
                    case AlertType.Price:
                        currentValue = alert.Ticker.CurrentPrice;  // Snapshot TRƯỚC khi check
                        shouldTrigger = CheckPriceAlert(alert);
                        break;
                    case AlertType.Volume:
                        currentValue = alert.Ticker.Volume ?? 0;
                        shouldTrigger = CheckVolumeAlert(alert);
                        break;
                    // Add more alert types as needed
                }

                if (shouldTrigger)
                {
                    await TriggerAlertAsync(alert, currentValue, unitOfWork, cancellationToken);  // Pass snapshot
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in CheckAlertsAsync");
        }
    }

    private bool CheckPriceAlert(Domain.Entities.Alert alert)
    {
        if (alert.Ticker == null || !alert.Threshold.HasValue)
            return false;

        try
        {
            var condition = JsonSerializer.Deserialize<AlertCondition>(alert.Condition);
            if (condition == null) return false;

            var currentPrice = alert.Ticker.CurrentPrice;

            return condition.Operator?.ToLower() switch
            {
                ">" or "above" or "greater" => currentPrice > alert.Threshold.Value,
                "<" or "below" or "less" => currentPrice < alert.Threshold.Value,
                ">=" => currentPrice >= alert.Threshold.Value,
                "<=" => currentPrice <= alert.Threshold.Value,
                "=" or "equals" => Math.Abs(currentPrice - alert.Threshold.Value) < 0.01m,
                _ => false
            };
        }
        catch
        {
            return false;
        }
    }

    private bool CheckVolumeAlert(Domain.Entities.Alert alert)
    {
        if (alert.Ticker == null || !alert.Threshold.HasValue || !alert.Ticker.Volume.HasValue)
            return false;

        return alert.Ticker.Volume.Value > (long)alert.Threshold.Value;
    }

    private async Task TriggerAlertAsync(
        Domain.Entities.Alert alert,
        decimal currentValue,  // Runtime snapshot
        StockInvestment.Application.Interfaces.IUnitOfWork unitOfWork,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation("Triggering alert {AlertId} for user {UserId}", alert.Id, alert.UserId);

        // Get operator trực tiếp từ alert condition
        var operatorStr = GetOperator(alert);

        // Build context
        var context = new AlertTriggeredContext
        {
            Alert = alert,
            UserId = alert.UserId,
            CurrentValue = currentValue,
            TriggeredAt = DateTime.UtcNow,
            Operator = operatorStr,
            MatchedCondition = $"{alert.Type} {operatorStr} {alert.Threshold}"
            // AiExplanation has default value "AI explanation unavailable", will be overwritten below
        };

        // Get AI Explanation với timeout
        try
        {
            using var aiCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            aiCts.CancelAfter(TimeSpan.FromSeconds(3));

            using var scope = _serviceProvider.CreateScope();
            var aiService = scope.ServiceProvider.GetRequiredService<IAIService>();

            var explanation = await aiService.GetAlertExplanationAsync(
                alert.Ticker?.Symbol ?? "",
                alert.Type.ToString(),
                currentValue,
                alert.Threshold ?? 0,
                aiCts.Token
            );

            context.AiExplanation = explanation ?? "AI explanation unavailable";
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to get AI explanation for alert {AlertId}", alert.Id);
            context.AiExplanation = "AI explanation unavailable";
        }

        // Persist TriggeredAt TRƯỚC khi gửi notifications (anti-spam)
        alert.TriggeredAt = context.TriggeredAt;
        alert.IsActive = false;
        await unitOfWork.SaveChangesAsync(cancellationToken);

        // Send SignalR notification
        try
        {
            using var scope = _serviceProvider.CreateScope();
            var hubContext = scope.ServiceProvider.GetRequiredService<IHubContext<TradingHub>>();

            // camelCase payload cho frontend
            var notification = new
            {
                alertId = alert.Id,
                symbol = alert.Ticker?.Symbol ?? "Unknown",
                tickerName = alert.Ticker?.Name ?? "Unknown",
                type = alert.Type.ToString(),
                threshold = alert.Threshold,
                currentValue = context.CurrentValue,
                triggeredAt = alert.TriggeredAt,
                aiExplanation = context.AiExplanation
            };

            // Event name: "AlertTriggered"
            await hubContext.Clients.User(alert.UserId.ToString())
                .SendAsync("AlertTriggered", notification, cancellationToken);

            _logger.LogInformation("SignalR notification sent for alert {AlertId}", alert.Id);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send SignalR notification for alert {AlertId}", alert.Id);
        }

        // Send external notifications (Slack/Telegram)
        try
        {
            using var scope = _serviceProvider.CreateScope();
            var notificationService = scope.ServiceProvider.GetRequiredService<INotificationChannelService>();

            await notificationService.SendAlertNotificationAsync(context, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send external notifications for alert {AlertId}", alert.Id);
            // Don't fail the alert trigger
        }

        _logger.LogInformation("Alert {AlertId} triggered successfully", alert.Id);
    }

    private string GetOperator(Domain.Entities.Alert alert)
    {
        try
        {
            var condition = JsonSerializer.Deserialize<AlertCondition>(alert.Condition);
            if (condition?.Operator == null)
                return ">";

            // Check >= and <= BEFORE > and <
            var op = condition.Operator.ToLower();
            if (op.Contains(">=")) return ">=";
            if (op.Contains("<=")) return "<=";
            if (op.Contains(">") || op == "above" || op == "greater") return ">";
            if (op.Contains("<") || op == "below" || op == "less") return "<";
            if (op == "=" || op == "equals") return "=";

            return ">";
        }
        catch
        {
            return ">";
        }
    }
}

internal class AlertCondition
{
    public string? Operator { get; set; }
    public decimal Value { get; set; }
}

