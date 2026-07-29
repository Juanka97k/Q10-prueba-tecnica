using System.Text;
using System.Text.Json;
using RabbitMQ.Client;
using OrderFlow.Shared.Events;

namespace OrderFlow.Api.Services;

public class RabbitMQPublisher : IMessagePublisher
{
    private readonly IConfiguration _configuration;

    public RabbitMQPublisher(IConfiguration configuration)
    {
        _configuration = configuration;
    }

    public async Task PublishOrderCreatedAsync(OrderCreatedIntegrationEvent @event)
    {
        var host = _configuration["RabbitMQ:Host"] ?? _configuration["RABBITMQ_HOST"] ?? "localhost";
        var user = _configuration["RabbitMQ:Username"] ?? _configuration["RABBITMQ_USER"] ?? "guest";
        var pass = _configuration["RabbitMQ:Password"] ?? _configuration["RABBITMQ_PASSWORD"] ?? "guest";
        var queue = _configuration["RabbitMQ:QueueName"] ?? _configuration["RABBITMQ_QUEUE_NAME"] ?? "order-created-queue";
        var portStr = _configuration["RabbitMQ:Port"] ?? _configuration["RABBITMQ_PORT"];

        var factory = new ConnectionFactory
        {
            HostName = host,
            UserName = user,
            Password = pass
        };

        if (int.TryParse(portStr, out var port))
        {
            factory.Port = port;
        }

        using var connection = await factory.CreateConnectionAsync();
        using var channel = await connection.CreateChannelAsync();

        // Aseguramos que la cola exista antes de publicar
        await channel.QueueDeclareAsync(
            queue: queue,
            durable: true,
            exclusive: false,
            autoDelete: false,
            arguments: null
        );

        var jsonMessage = JsonSerializer.Serialize(@event);
        var body = Encoding.UTF8.GetBytes(jsonMessage);

        var properties = new BasicProperties
        {
            Persistent = true // Mensaje persistente para que no se pierda si cae el broker
        };

        await channel.BasicPublishAsync(
            exchange: string.Empty,
            routingKey: queue,
            mandatory: true,
            basicProperties: properties,
            body: body
        );
    }
}