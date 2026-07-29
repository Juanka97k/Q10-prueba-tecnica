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
        var factory = new ConnectionFactory
        {
            HostName = _configuration["RabbitMQ:Host"] ?? "localhost",
            UserName = _configuration["RabbitMQ:Username"] ?? "guest",
            Password = _configuration["RabbitMQ:Password"] ?? "guest"
        };

        using var connection = await factory.CreateConnectionAsync();
        using var channel = await connection.CreateChannelAsync();

        // Aseguramos que la cola exista antes de publicar
        await channel.QueueDeclareAsync(
            queue: "order-created-queue",
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
            routingKey: "order-created-queue",
            mandatory: true,
            basicProperties: properties,
            body: body
        );
    }
}