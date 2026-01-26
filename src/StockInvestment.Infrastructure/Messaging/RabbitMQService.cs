using Microsoft.Extensions.Logging;
using RabbitMQ.Client;
using System.Text;
using System.Text.Json;

namespace StockInvestment.Infrastructure.Messaging;

public class RabbitMQService : IDisposable
{
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

        // Declare queues
        _channel.QueueDeclare(queue: "news_summarize", durable: true, exclusive: false, autoDelete: false);
        _channel.QueueDeclare(queue: "event_analyze", durable: true, exclusive: false, autoDelete: false);
        _channel.QueueDeclare(queue: "forecast_generate", durable: true, exclusive: false, autoDelete: false);
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

