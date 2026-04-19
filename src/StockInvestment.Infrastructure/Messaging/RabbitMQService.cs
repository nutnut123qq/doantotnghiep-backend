using Microsoft.Extensions.Logging;
using RabbitMQ.Client;
using System.Text;
using System.Text.Json;

namespace StockInvestment.Infrastructure.Messaging;

public class RabbitMQService : IDisposable
{
    // Queue names kept as constants so publishers and consumers cannot drift.
    public const string NewsSummarizeQueue = "news_summarize.v2";
    public const string NewsSummarizeDlx = "news_summarize.dlx";
    public const string NewsSummarizeDlq = "news_summarize.dlq";
    public const string NewsSummarizeDlqRoutingKey = "dead";

    private readonly IConnection _connection;
    private readonly IModel _channel;
    private readonly ILogger<RabbitMQService> _logger;

    public IModel Channel => _channel;

    public RabbitMQService(ILogger<RabbitMQService> logger, string connectionString)
    {
        _logger = logger;
        var factory = new ConnectionFactory
        {
            Uri = new Uri(connectionString)
        };

        _connection = factory.CreateConnection();
        _channel = _connection.CreateModel();

        DeclareNewsSummarizeTopology();

        // Other queues unrelated to the summarization flow — left unchanged.
        _channel.QueueDeclare(queue: "event_analyze", durable: true, exclusive: false, autoDelete: false);
        _channel.QueueDeclare(queue: "forecast_generate", durable: true, exclusive: false, autoDelete: false);
    }

    /// <summary>
    /// Declares the summarization topology: a dead-letter exchange + DLQ, and the
    /// primary queue wired to dead-letter on <c>BasicNack(requeue:false)</c>.
    /// A new queue name (<c>news_summarize.v2</c>) is used so we can add DLX
    /// arguments without triggering <c>PRECONDITION_FAILED</c> against the
    /// pre-existing <c>news_summarize</c> queue.
    /// </summary>
    private void DeclareNewsSummarizeTopology()
    {
        _channel.ExchangeDeclare(
            exchange: NewsSummarizeDlx,
            type: ExchangeType.Direct,
            durable: true,
            autoDelete: false);

        _channel.QueueDeclare(
            queue: NewsSummarizeDlq,
            durable: true,
            exclusive: false,
            autoDelete: false);

        _channel.QueueBind(
            queue: NewsSummarizeDlq,
            exchange: NewsSummarizeDlx,
            routingKey: NewsSummarizeDlqRoutingKey);

        var args = new Dictionary<string, object>
        {
            ["x-dead-letter-exchange"] = NewsSummarizeDlx,
            ["x-dead-letter-routing-key"] = NewsSummarizeDlqRoutingKey,
        };

        _channel.QueueDeclare(
            queue: NewsSummarizeQueue,
            durable: true,
            exclusive: false,
            autoDelete: false,
            arguments: args);
    }

    public void Publish<T>(string queueName, T message)
    {
        try
        {
            var json = JsonSerializer.Serialize(message);
            var body = Encoding.UTF8.GetBytes(json);

            var properties = _channel.CreateBasicProperties();
            properties.Persistent = true;

            _channel.BasicPublish(
                exchange: "",
                routingKey: queueName,
                basicProperties: properties,
                body: body);

            _logger.LogInformation("Message published to queue: {QueueName}", queueName);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error publishing message to queue: {QueueName}", queueName);
            throw;
        }
    }

    public void Dispose()
    {
        _channel?.Close();
        _connection?.Close();
        _channel?.Dispose();
        _connection?.Dispose();
    }
}
