using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System;

namespace StockInvestment.Infrastructure.Messaging.MessageHandlers;

/// <summary>
/// Background service to start RabbitMQ consumer for news summarization.
/// </summary>
public class NewsSummarizeConsumerService : BackgroundService
{
    private readonly NewsSummarizeHandler _handler;
    private readonly IConfiguration _configuration;
    private readonly ILogger<NewsSummarizeConsumerService> _logger;

    public NewsSummarizeConsumerService(
        NewsSummarizeHandler handler,
        IConfiguration configuration,
        ILogger<NewsSummarizeConsumerService> logger)
    {
        _handler = handler;
        _configuration = configuration;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var enabled = _configuration.GetValue<bool>("RabbitMQ:Enabled");
        if (!enabled)
        {
            _logger.LogInformation("RabbitMQ is disabled. News summarization consumer will not start.");
            return;
        }

        _logger.LogInformation("Starting NewsSummarize consumer...");
        _handler.StartConsuming();

        // Keep the background service alive
        await Task.Delay(Timeout.InfiniteTimeSpan, stoppingToken);
    }
}
