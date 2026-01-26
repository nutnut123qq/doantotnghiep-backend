using Microsoft.Extensions.Logging;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using StockInvestment.Application.Interfaces;
using StockInvestment.Domain.Enums;
using System.Text;
using System.Text.Json;

namespace StockInvestment.Infrastructure.Messaging.MessageHandlers;

public class NewsSummarizeHandler
{
    private readonly IModel _channel;
    private readonly IAIService _aiService;
    private readonly INewsService _newsService;
    private readonly ILogger<NewsSummarizeHandler> _logger;

    public NewsSummarizeHandler(
        IModel channel,
        IAIService aiService,
        INewsService newsService,
        ILogger<NewsSummarizeHandler> logger)
    {
        _channel = channel;
        _aiService = aiService;
        _newsService = newsService;
        _logger = logger;
    }

    public void StartConsuming()
    {
        var consumer = new EventingBasicConsumer(_channel);
        consumer.Received += async (model, ea) =>
        {
            var body = ea.Body.ToArray();
            var message = Encoding.UTF8.GetString(body);
            
            try
            {
                var request = JsonSerializer.Deserialize<SummarizeRequest>(message);
                if (request == null)
                {
                    _logger.LogWarning("Invalid summarization message: {Message}", message);
                    return;
                }

                _logger.LogInformation("Processing summarization request for news {NewsId}", request.NewsId);

                var news = await _newsService.GetNewsByIdAsync(request.NewsId);
                if (news == null)
                {
                    _logger.LogWarning("News {NewsId} not found, skipping summarization", request.NewsId);
                    return;
                }

                if (string.IsNullOrWhiteSpace(news.Content))
                {
                    _logger.LogWarning("News {NewsId} has empty content, skipping summarization", request.NewsId);
                    return;
                }

                var summaryResult = await _aiService.SummarizeNewsDetailedAsync(news.Content);

                news.Summary = summaryResult.Summary;
                news.Sentiment = ParseSentiment(summaryResult.Sentiment);
                news.ImpactAssessment = summaryResult.ImpactAssessment;

                await _newsService.UpdateNewsAsync(news);

                _logger.LogInformation("Summarization completed for news {NewsId}", request.NewsId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing summarization message");
            }
            finally
            {
                _channel.BasicAck(deliveryTag: ea.DeliveryTag, multiple: false);
            }
        };

        _channel.BasicConsume(queue: "news_summarize", autoAck: false, consumer: consumer);
        _logger.LogInformation("Started consuming news summarization messages");
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

