using System.Text;
using System.Text.Json;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using OrderFlow.InventoryWorker.Services;
using OrderFlow.Shared.Events;

namespace OrderFlow.InventoryWorker;

public class Worker : BackgroundService
{
    private readonly IConfiguration _configuration;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<Worker> _logger;

    public Worker(
        IConfiguration configuration,
        IServiceScopeFactory scopeFactory,
        ILogger<Worker> logger)
    {
        _configuration = configuration;
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
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

        // Reintentos de conexión por si RabbitMQ tarda en arrancar en Docker
        IConnection connection = null!;
        IChannel channel = null!;

        while (!stoppingToken.IsCancellationRequested && connection == null)
        {
            try
            {
                connection = await factory.CreateConnectionAsync(stoppingToken);
                channel = await connection.CreateChannelAsync(cancellationToken: stoppingToken);
                _logger.LogInformation("Conexión exitosa con RabbitMQ desde InventoryWorker.");
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "RabbitMQ aún no está listo. Reintentando en 5 segundos...");
                await Task.Delay(5000, stoppingToken);
            }
        }

        if (stoppingToken.IsCancellationRequested) return;

        // Declara la cola para asegurarse de que exista
        await channel.QueueDeclareAsync(
            queue: queue,
            durable: true,
            exclusive: false,
            autoDelete: false,
            arguments: null,
            cancellationToken: stoppingToken
        );

        // Limita el Prefetch para procesar de a 1 mensaje a la vez por Worker (Fair Dispatch)
        await channel.BasicQosAsync(prefetchSize: 0, prefetchCount: 1, global: false, cancellationToken: stoppingToken);

        var consumer = new AsyncEventingBasicConsumer(channel);

        consumer.ReceivedAsync += async (model, ea) =>
        {
            var body = ea.Body.ToArray();
            var message = Encoding.UTF8.GetString(body);

            try
            {
                var integrationEvent = JsonSerializer.Deserialize<OrderCreatedIntegrationEvent>(message);

                if (integrationEvent != null)
                {
                    _logger.LogInformation("Evento recibido desde RabbitMQ: EventId {EventId}, OrderId {OrderId}",
                        integrationEvent.EventId, integrationEvent.OrderId);

                    // Crear un Scope fresco para resolver los servicios Scoped (DbContext)
                    using var scope = _scopeFactory.CreateScope();
                    var processor = scope.ServiceProvider.GetRequiredService<IInventoryProcessor>();

                    await processor.ProcessOrderCreatedAsync(integrationEvent, stoppingToken);
                }

                // Confirmación exitosa a RabbitMQ (Quita el mensaje de la cola)
                await channel.BasicAckAsync(deliveryTag: ea.DeliveryTag, multiple: false, cancellationToken: stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error procesando el mensaje de RabbitMQ. Se enviará Nack para reintento.");

                // Enviar Nack requeueando el mensaje si hubo fallo inesperado de infraestructura
                await channel.BasicNackAsync(deliveryTag: ea.DeliveryTag, multiple: false, requeue: true, cancellationToken: stoppingToken);
            }
        };

        await channel.BasicConsumeAsync(
            queue: queue,
            autoAck: false, // ACK manual para garantizar resiliencia
            consumer: consumer,
            cancellationToken: stoppingToken
        );

        _logger.LogInformation("InventoryWorker está escuchando activamente en la cola '{Queue}'.", queue);

        // Mantener vivo el servicio en segundo plano
        while (!stoppingToken.IsCancellationRequested)
        {
            await Task.Delay(1000, stoppingToken);
        }
    }
}