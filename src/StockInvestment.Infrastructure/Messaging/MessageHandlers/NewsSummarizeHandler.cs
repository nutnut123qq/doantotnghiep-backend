using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using StockInvestment.Application.Interfaces;
using StockInvestment.Domain.Enums;
using System.Text;
using System.Text.Json;

namespace StockInvestment.Infrastructure.Messaging.MessageHandlers;

/// <summary>
/// Consumes news summarization jobs from RabbitMQ.
///
/// Reliability contract:
/// - One DI scope is created per message (no captive dependency; DbContext and
///   AI clients are fresh for every delivery).
/// - Permanent failures (bad payload / missing news / empty content) are Ack'd
///   immediately — re-delivery would not help.
/// - Transient failures are retried by republishing the message with an
///   incremented <c>x-retry-count</c> header up to <c>RabbitMQ:NewsSummarize:MaxRetries</c>.
/// - When retries are exhausted the message is Nack'd with <c>requeue=false</c>,
///   so the queue's configured DLX routes it to <c>news_summarize.dlq</c>.
/// </summary>
public class NewsSummarizeHandler
{
    private const string RetryCountHeader = "x-retry-count";
    private const int DefaultMaxRetries = 3;

    private readonly IModel _channel;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IConfiguration _configuration;
    private readonly ILogger<NewsSummarizeHandler> _logger;

    public NewsSummarizeHandler(
        IModel channel,
        IServiceScopeFactory scopeFactory,
        IConfiguration configuration,
        ILogger<NewsSummarizeHandler> logger)
    {
        _channel = channel;
        _scopeFactory = scopeFactory;
        _configuration = configuration;
        _logger = logger;
    }

    public void StartConsuming()
    {
        var consumer = new EventingBasicConsumer(_channel);
        consumer.Received += async (_, ea) => await HandleMessageAsync(ea);
        _channel.BasicConsume(
            queue: RabbitMQService.NewsSummarizeQueue,
            autoAck: false,
            consumer: consumer);
        _logger.LogInformation(
            "Started consuming news summarization messages from {Queue}",
            RabbitMQService.NewsSummarizeQueue);
    }

    private async Task HandleMessageAsync(BasicDeliverEventArgs ea)
    {
        var body = ea.Body.ToArray();
        var message = Encoding.UTF8.GetString(body);
        var retryCount = GetRetryCount(ea.BasicProperties);
        var maxRetries = _configuration.GetValue<int>(
            "RabbitMQ:NewsSummarize:MaxRetries",
            DefaultMaxRetries);

        SummarizeRequest? request;
        try
        {
            request = JsonSerializer.Deserialize<SummarizeRequest>(message);
        }
        catch (JsonException ex)
        {
            _logger.LogWarning(ex, "Invalid JSON on news_summarize: {Message}", message);
            SafeAck(ea.DeliveryTag);
            return;
        }

        if (request == null || request.NewsId == Guid.Empty)
        {
            _logger.LogWarning("Invalid summarization payload (null or empty NewsId): {Message}", message);
            SafeAck(ea.DeliveryTag);
            return;
        }

        try
        {
            using var scope = _scopeFactory.CreateScope();
            var newsService = scope.ServiceProvider.GetRequiredService<INewsService>();
            var aiService = scope.ServiceProvider.GetRequiredService<IAIService>();

            var news = await newsService.GetNewsByIdAsync(request.NewsId);
            if (news == null)
            {
                _logger.LogWarning("News {NewsId} not found, skipping summarization", request.NewsId);
                SafeAck(ea.DeliveryTag);
                return;
            }

            if (string.IsNullOrWhiteSpace(news.Content))
            {
                _logger.LogWarning("News {NewsId} has empty content, skipping summarization", request.NewsId);
                SafeAck(ea.DeliveryTag);
                return;
            }

            _logger.LogInformation(
                "Processing summarization request for news {NewsId} (retry={Retry}/{Max})",
                request.NewsId,
                retryCount,
                maxRetries);

            var summaryResult = await aiService.SummarizeNewsDetailedAsync(news.Content);

            news.Summary = summaryResult.Summary;
            news.Sentiment = ParseSentiment(summaryResult.Sentiment);
            news.ImpactAssessment = summaryResult.ImpactAssessment;

            await newsService.UpdateNewsAsync(news);

            _logger.LogInformation("Summarization completed for news {NewsId}", request.NewsId);
            SafeAck(ea.DeliveryTag);
        }
        catch (Exception ex)
        {
            // Treat as transient; decide retry vs dead-letter based on attempt count.
            if (retryCount >= maxRetries)
            {
                _logger.LogError(
                    ex,
                    "Summarization failed permanently for news {NewsId} after {Attempts} attempts. Dead-lettering.",
                    request.NewsId,
                    retryCount);
                SafeNack(ea.DeliveryTag, requeue: false);
                return;
            }

            _logger.LogWarning(
                ex,
                "Summarization failed for news {NewsId}, scheduling retry {Next}/{Max}",
                request.NewsId,
                retryCount + 1,
                maxRetries);

            try
            {
                RepublishWithRetry(ea, retryCount + 1);
                SafeAck(ea.DeliveryTag);
            }
            catch (Exception republishEx)
            {
                // If republish itself fails, requeue so the broker redelivers.
                _logger.LogError(
                    republishEx,
                    "Failed to republish news {NewsId} for retry; requeueing original message",
                    request.NewsId);
                SafeNack(ea.DeliveryTag, requeue: true);
            }
        }
    }

    private void RepublishWithRetry(BasicDeliverEventArgs ea, int nextRetryCount)
    {
        var properties = _channel.CreateBasicProperties();
        properties.Persistent = true;
        properties.ContentType = ea.BasicProperties?.ContentType;
        properties.MessageId = ea.BasicProperties?.MessageId ?? Guid.NewGuid().ToString("N");

        var headers = new Dictionary<string, object>();
        if (ea.BasicProperties?.Headers != null)
        {
            foreach (var kv in ea.BasicProperties.Headers)
            {
                if (kv.Key == RetryCountHeader) continue;
                headers[kv.Key] = kv.Value;
            }
        }
        headers[RetryCountHeader] = nextRetryCount;
        properties.Headers = headers;

        _channel.BasicPublish(
            exchange: string.Empty,
            routingKey: RabbitMQService.NewsSummarizeQueue,
            basicProperties: properties,
            body: ea.Body);
    }

    private static int GetRetryCount(IBasicProperties? props)
    {
        if (props?.Headers == null) return 0;
        if (!props.Headers.TryGetValue(RetryCountHeader, out var raw) || raw == null) return 0;

        return raw switch
        {
            int i => i,
            long l => (int)l,
            byte[] bytes => int.TryParse(Encoding.UTF8.GetString(bytes), out var parsed) ? parsed : 0,
            string s => int.TryParse(s, out var parsed) ? parsed : 0,
            _ => 0,
        };
    }

    private void SafeAck(ulong deliveryTag)
    {
        try
        {
            _channel.BasicAck(deliveryTag, multiple: false);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "BasicAck failed for deliveryTag {Tag}", deliveryTag);
        }
    }

    private void SafeNack(ulong deliveryTag, bool requeue)
    {
        try
        {
            _channel.BasicNack(deliveryTag, multiple: false, requeue: requeue);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "BasicNack failed for deliveryTag {Tag}", deliveryTag);
        }
    }

    private record SummarizeRequest(Guid NewsId);

    private static Sentiment? ParseSentiment(string sentimentString)
    {
        if (string.IsNullOrWhiteSpace(sentimentString))
            return null;

        return sentimentString.ToLower() switch
        {
            "positive" => Sentiment.Positive,
            "negative" => Sentiment.Negative,
            "neutral" => Sentiment.Neutral,
            _ => Sentiment.Neutral
        };
    }
}
