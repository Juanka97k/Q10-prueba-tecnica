using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.SignalR;
using OrderFlow.Api.Hubs;
using OrderFlow.Shared.Events;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace OrderFlow.Api.Services;

public class OrderProcessedConsumerService : BackgroundService
{
    private readonly IConfiguration _configuration;
    private readonly IHubContext<OrdersHub> _hubContext;
    private readonly ILogger<OrderProcessedConsumerService> _logger;

    public OrderProcessedConsumerService(
        IConfiguration configuration,
        IHubContext<OrdersHub> hubContext,
        ILogger<OrderProcessedConsumerService> logger)
    {
        _configuration = configuration;
        _hubContext = hubContext;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var factory = new ConnectionFactory
        {
            HostName = _configuration["RabbitMQ:Host"] ?? "localhost",
            UserName = _configuration["RabbitMQ:Username"] ?? "guest",
            Password = _configuration["RabbitMQ:Password"] ?? "guest"
        };

        IConnection connection = null!;
        IChannel channel = null!;

        while (!stoppingToken.IsCancellationRequested && connection == null)
        {
            try
            {
                connection = await factory.CreateConnectionAsync(stoppingToken);
                channel = await connection.CreateChannelAsync(cancellationToken: stoppingToken);
            }
            catch
            {
                await Task.Delay(5000, stoppingToken);
            }
        }

        if (stoppingToken.IsCancellationRequested) return;

        await channel.QueueDeclareAsync(
            queue: "order-processed-queue",
            durable: true,
            exclusive: false,
            autoDelete: false,
            arguments: null,
            cancellationToken: stoppingToken
        );

        var consumer = new AsyncEventingBasicConsumer(channel);

        consumer.ReceivedAsync += async (model, ea) =>
        {
            var body = ea.Body.ToArray();
            var message = Encoding.UTF8.GetString(body);

            try
            {
                var processedEvent = JsonSerializer.Deserialize<OrderProcessedIntegrationEvent>(message);

                if (processedEvent != null)
                {
                    _logger.LogInformation("API recibió OrderProcessedIntegrationEvent para la orden {OrderId}. Emitiendo WebSocket...", processedEvent.OrderId);

                    // 🚀 EMITIR EVENTO WEBSOCKET A TODOS LOS CLIENTES CONECTADOS
                    await _hubContext.Clients.All.SendAsync("OrderUpdated", processedEvent, cancellationToken: stoppingToken);
                }

                await channel.BasicAckAsync(deliveryTag: ea.DeliveryTag, multiple: false, cancellationToken: stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error procesando mensaje en OrderProcessedConsumerService.");
                await channel.BasicNackAsync(deliveryTag: ea.DeliveryTag, multiple: false, requeue: true, cancellationToken: stoppingToken);
            }
        };

        await channel.BasicConsumeAsync(
            queue: "order-processed-queue",
            autoAck: false,
            consumer: consumer,
            cancellationToken: stoppingToken
        );
    }
}
