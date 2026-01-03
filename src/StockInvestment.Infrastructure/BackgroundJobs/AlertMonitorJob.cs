using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using StockInvestment.Domain.Enums;
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

                bool shouldTrigger = false;

                switch (alert.Type)
                {
                    case AlertType.Price:
                        shouldTrigger = CheckPriceAlert(alert);
                        break;
                    case AlertType.Volume:
                        shouldTrigger = CheckVolumeAlert(alert);
                        break;
                    // Add more alert types as needed
                }

                if (shouldTrigger)
                {
                    await TriggerAlertAsync(alert, unitOfWork, cancellationToken);
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

    private async Task TriggerAlertAsync(Domain.Entities.Alert alert, StockInvestment.Application.Interfaces.IUnitOfWork unitOfWork, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Triggering alert {AlertId} for user {UserId}", alert.Id, alert.UserId);

        alert.TriggeredAt = DateTime.UtcNow;
        alert.IsActive = false; // Deactivate after triggering

        await unitOfWork.SaveChangesAsync(cancellationToken);

        // TODO: Send notification (Slack, Telegram, Email, etc.)
        _logger.LogInformation("Alert {AlertId} triggered: {Symbol} {Type} {Threshold}",
            alert.Id, alert.Ticker?.Symbol, alert.Type, alert.Threshold);
    }
}

internal class AlertCondition
{
    public string? Operator { get; set; }
    public decimal Value { get; set; }
}

