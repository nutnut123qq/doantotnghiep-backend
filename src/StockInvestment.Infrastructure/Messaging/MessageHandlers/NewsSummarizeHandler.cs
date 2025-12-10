using Microsoft.Extensions.Logging;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using StockInvestment.Application.Interfaces;
using System.Text;
using System.Text.Json;

namespace StockInvestment.Infrastructure.Messaging.MessageHandlers;

public class NewsSummarizeHandler
{
    private readonly IModel _channel;
    private readonly IAIService _aiService;
    private readonly ILogger<NewsSummarizeHandler> _logger;

    public NewsSummarizeHandler(IModel channel, IAIService aiService, ILogger<NewsSummarizeHandler> logger)
    {
        _channel = channel;
        _aiService = aiService;
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
                if (request != null)
                {
                    _logger.LogInformation("Processing summarization request for news {NewsId}", request.NewsId);
                    
                    // TODO: Fetch news content from database
                    // For now, using mock content
                    var newsContent = "Sample news content to be summarized";
                    
                    var summary = await _aiService.SummarizeNewsAsync(newsContent);
                    
                    _logger.LogInformation("Summarization completed for news {NewsId}", request.NewsId);
                    
                    // TODO: Update news record in database with summary
                }
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
}

