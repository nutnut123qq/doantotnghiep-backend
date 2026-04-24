using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using StockInvestment.Application.Contracts.Notifications;
using StockInvestment.Application.Interfaces;
using StockInvestment.Domain.Enums;
using StockInvestment.Infrastructure.Hubs;
using System.Diagnostics;
using System.Diagnostics.Metrics;
using System.Text.Json;

namespace StockInvestment.Infrastructure.BackgroundJobs;

/// <summary>
/// Background job to monitor and trigger alerts
/// P1-2: Uses distributed lock to prevent duplicate execution across instances
/// </summary>
public class AlertMonitorJob : BackgroundService
{
    private static readonly Meter JobMeter = new("StockInvestment.BackgroundJobs");
    private static readonly Counter<long> JobSuccessCounter = JobMeter.CreateCounter<long>("job_runs_success");
    private static readonly Counter<long> JobFailureCounter = JobMeter.CreateCounter<long>("job_runs_failure");
    private static readonly Counter<long> JobLockSkippedCounter = JobMeter.CreateCounter<long>("job_runs_lock_skipped");
    private readonly ILogger<AlertMonitorJob> _logger;
    private readonly IServiceProvider _serviceProvider;
    private readonly IConfiguration _configuration;
    private readonly bool _enableExternalChannels;
    private readonly TimeSpan _checkInterval = TimeSpan.FromMinutes(1);

    public AlertMonitorJob(
        ILogger<AlertMonitorJob> logger,
        IServiceProvider serviceProvider,
        IConfiguration configuration)
    {
        _logger = logger;
        _serviceProvider = serviceProvider;
        _configuration = configuration;
        _enableExternalChannels = _configuration.GetValue<bool>("Notifications:EnableExternalChannels");
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
        var runId = Guid.NewGuid().ToString("N");
        var stopwatch = Stopwatch.StartNew();
        const string jobName = "alert-monitor";
        var runSucceeded = false;
        using var scope = _serviceProvider.CreateScope();
        
        var distributedLock = await JobLockHelper.TryAcquireLockAsync(
            scope, _configuration, _logger, jobName, TimeSpan.FromMinutes(5), cancellationToken);
        
        if (distributedLock == null)
        {
            JobLockSkippedCounter.Add(1, new KeyValuePair<string, object?>("jobName", jobName));
            _logger.LogInformation("Background job skipped: {jobName} runId={runId} lockAcquired={lockAcquired} result={result}",
                jobName, runId, false, "skipped_lock");
            return;
        }

        try
        {
            var unitOfWork = scope.ServiceProvider.GetRequiredService<StockInvestment.Application.Interfaces.IUnitOfWork>();
            var activeAlerts = await unitOfWork.Alerts.GetActiveAlertsWithTickerAndUserAsync(cancellationToken);

            _logger.LogInformation("Background job running: {jobName} runId={runId} lockAcquired={lockAcquired} activeAlerts={activeAlerts}",
                jobName, runId, true, activeAlerts.Count());

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

                if (!Enum.IsDefined(typeof(AlertType), alert.Type))
                {
                    _logger.LogWarning(
                        "Alert {AlertId} for {Symbol} skipped: unsupported type {AlertType}. Only Price and Volume alerts are supported.",
                        alert.Id,
                        alert.Ticker.Symbol,
                        alert.Type);
                    continue;
                }

                switch (alert.Type)
                {
                    case AlertType.Price:
                        currentValue = alert.Ticker.CurrentPrice;  // Snapshot TRƯỚC khi check
                        if (currentValue == 0)
                        {
                            _logger.LogWarning(
                                "Price alert {AlertId} for {Symbol} skipped: CurrentPrice is 0 (check AI/market quote service and StockPriceUpdateJob).",
                                alert.Id,
                                alert.Ticker.Symbol);
                        }
                        shouldTrigger = CheckPriceAlert(alert);
                        if (!shouldTrigger && alert.Threshold.HasValue)
                        {
                            _logger.LogDebug(
                                "Price alert {AlertId} {Symbol} not triggered: price={Price} threshold={Threshold} op={Op}",
                                alert.Id,
                                alert.Ticker.Symbol,
                                currentValue,
                                alert.Threshold.Value,
                                GetOperator(alert));
                        }
                        break;
                    case AlertType.Volume:
                        currentValue = alert.Ticker.Volume ?? 0;
                        shouldTrigger = CheckVolumeAlert(alert);
                        break;
                }

                if (shouldTrigger)
                {
                    // P0-3: Try to atomically mark alert as triggered to prevent duplicate notifications
                    // Only proceed if this instance successfully claimed the alert
                    var claimed = await unitOfWork.Alerts.TryMarkAsTriggeredAsync(alert.Id, DateTime.UtcNow, cancellationToken);
                    if (!claimed)
                    {
                        _logger.LogDebug("Alert {AlertId} was already triggered by another instance, skipping", alert.Id);
                        continue;
                    }

                    // Reload alert to get updated state
                    alert.IsActive = false;
                    alert.TriggeredAt = DateTime.UtcNow;
                    
                    await TriggerAlertAsync(alert, currentValue, unitOfWork, cancellationToken);  // Pass snapshot
                }
            }
            runSucceeded = true;
        }
        catch (Exception ex)
        {
            JobFailureCounter.Add(1, new KeyValuePair<string, object?>("jobName", jobName));
            _logger.LogError(ex, "Background job failed: {jobName} runId={runId} lockAcquired={lockAcquired} durationMs={durationMs} result={result}",
                jobName, runId, true, stopwatch.ElapsedMilliseconds, "failed");
        }
        finally
        {
            // P1-2: Release distributed lock
            if (distributedLock != null)
            {
                await distributedLock.ReleaseAsync();
                distributedLock.Dispose();
            }
            if (runSucceeded && !cancellationToken.IsCancellationRequested)
            {
                JobSuccessCounter.Add(1, new KeyValuePair<string, object?>("jobName", jobName));
                _logger.LogInformation("Background job completed: {jobName} runId={runId} lockAcquired={lockAcquired} durationMs={durationMs} result={result}",
                    jobName, runId, true, stopwatch.ElapsedMilliseconds, "completed");
            }
        }
    }

    private bool CheckPriceAlert(Domain.Entities.Alert alert)
    {
        if (alert.Ticker == null || !alert.Threshold.HasValue)
            return false;

        var currentPrice = alert.Ticker.CurrentPrice;
        // Frontend sends threshold in thousand-VND to keep payloads small;
        // CurrentPrice is stored in full VND after normalization.
        var thresholdVnd = alert.Threshold.Value * 1000m;
        var normalizedOperator = GetOperator(alert);

        return normalizedOperator switch
        {
            ">" => currentPrice > thresholdVnd,
            "<" => currentPrice < thresholdVnd,
            ">=" => currentPrice >= thresholdVnd,
            "<=" => currentPrice <= thresholdVnd,
            "=" => Math.Abs(currentPrice - thresholdVnd) < 0.01m,
            _ => false
        };
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
        };

        // P0-3: Alert is already marked as triggered atomically in CheckAlertsAsync
        // No need to update again here - just ensure state is saved if needed
        // (The atomic update already set IsActive=false and TriggeredAt, but we save any other changes)
        await unitOfWork.SaveChangesAsync(cancellationToken);

        // Send SignalR notification
        try
        {
            using var scope = _serviceProvider.CreateScope();
            var hubContext = scope.ServiceProvider.GetRequiredService<IHubContext<TradingHub>>();

            // camelCase payload cho frontend
            // Align price units: CurrentPrice is full VND; threshold is stored as thousand-VND.
            var displayThreshold = alert.Type == AlertType.Price && alert.Threshold.HasValue
                ? alert.Threshold.Value * 1000m
                : alert.Threshold;
            // CurrentValue already comes from Ticker.CurrentPrice which is stored in full VND.
            var displayCurrentValue = context.CurrentValue;

            var notification = new
            {
                alertId = alert.Id,
                symbol = alert.Ticker?.Symbol ?? "Unknown",
                tickerName = alert.Ticker?.Name ?? "Unknown",
                type = alert.Type.ToString(),
                threshold = displayThreshold,
                currentValue = displayCurrentValue,
                triggeredAt = alert.TriggeredAt
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

        // Send external notifications (Slack/Telegram) only when explicitly enabled
        if (_enableExternalChannels)
        {
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
        }
        else
        {
            _logger.LogInformation("External alert notifications disabled by configuration for alert {AlertId}", alert.Id);
        }

        _logger.LogInformation("Alert {AlertId} triggered successfully", alert.Id);
    }

    private string GetOperator(Domain.Entities.Alert alert)
    {
        if (string.IsNullOrWhiteSpace(alert.Condition))
            return ">";

        try
        {
            var condition = JsonSerializer.Deserialize<AlertCondition>(alert.Condition);
            if (!string.IsNullOrWhiteSpace(condition?.Operator))
                return NormalizeOperator(condition.Operator);
        }
        catch (Exception)
        {
            // Condition can be plain text values like "greater_than" from manual UI flow.
        }

        return NormalizeOperator(alert.Condition);
    }

    private static string NormalizeOperator(string rawOperator)
    {
        var op = rawOperator.Trim().ToLowerInvariant();

        return op switch
        {
            ">=" or "gte" or "greater_or_equal" or "greater_or_equal_than" => ">=",
            "<=" or "lte" or "less_or_equal" or "less_or_equal_than" => "<=",
            ">" or "above" or "greater" or "greater_than" => ">",
            "<" or "below" or "less" or "less_than" => "<",
            "=" or "equals" or "equal" => "=",
            _ => ">"
        };
    }
}

internal class AlertCondition
{
    public string? Operator { get; set; }
    public decimal Value { get; set; }
}

