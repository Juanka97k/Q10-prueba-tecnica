using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using OrderFlow.Infrastructure.Entities;
using OrderFlow.Infrastructure.Persistence;
using OrderFlow.Shared.Events;
using RabbitMQ.Client;

namespace OrderFlow.InventoryWorker.Services;

public interface IInventoryProcessor
{
    Task ProcessOrderCreatedAsync(OrderCreatedIntegrationEvent integrationEvent, CancellationToken cancellationToken);
}

public class InventoryProcessor : IInventoryProcessor
{
    private readonly OrderFlowDbContext _context;
    private readonly IConfiguration _configuration;
    private readonly ILogger<InventoryProcessor> _logger;

    public InventoryProcessor(
        OrderFlowDbContext context, 
        IConfiguration configuration,
        ILogger<InventoryProcessor> logger)
    {
        _context = context;
        _configuration = configuration;
        _logger = logger;
    }

    public async Task ProcessOrderCreatedAsync(OrderCreatedIntegrationEvent integrationEvent, CancellationToken cancellationToken)
    {
        var yaProcesado = await _context.ProcessedEvents
            .AnyAsync(p => p.EventId == integrationEvent.EventId, cancellationToken);

        if (yaProcesado)
        {
            _logger.LogWarning("Evento {EventId} ya fue procesado anteriormente. Se omite para garantizar Idempotencia.", integrationEvent.EventId);
            return;
        }

        using var transaction = await _context.Database.BeginTransactionAsync(cancellationToken);

        try
        {
            var pedido = await _context.Pedidos.FindAsync(new object[] { integrationEvent.OrderId }, cancellationToken);
            var stock = await _context.Stocks.FindAsync(new object[] { integrationEvent.Sku }, cancellationToken);

            if (pedido == null)
            {
                _logger.LogError("No se encontró el pedido {OrderId} asociado al evento.", integrationEvent.OrderId);
                return;
            }

            if (stock != null && stock.Disponible >= integrationEvent.Cantidad)
            {
                stock.Disponible -= integrationEvent.Cantidad;
                pedido.Estado = OrderStatus.Confirmed;
                _logger.LogInformation("Stock reservado exitosamente para Pedido {OrderId}.", pedido.Id);
            }
            else
            {
                pedido.Estado = OrderStatus.Rejected;
                _logger.LogWarning("Stock insuficiente para Pedido {OrderId}. Estado cambiado a Rejected.", pedido.Id);
            }

            _context.ProcessedEvents.Add(new ProcessedEvent
            {
                EventId = integrationEvent.EventId,
                ProcesadoEn = DateTime.UtcNow
            });

            await _context.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);

            // 📣 PUBLICAR EVENTO DE RESPUESTA A RABBITMQ PARA LA API
            await PublishOrderProcessedEventAsync(new OrderProcessedIntegrationEvent(
                OrderId: pedido.Id,
                Estado: pedido.Estado.ToString(),
                ProcesadoEn: DateTime.UtcNow
            ));
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync(cancellationToken);
            _logger.LogError(ex, "Error procesando el evento {EventId}. Transacción revertida.", integrationEvent.EventId);
            throw;
        }
    }

    private async Task PublishOrderProcessedEventAsync(OrderProcessedIntegrationEvent @event)
    {
        var factory = new ConnectionFactory
        {
            HostName = _configuration["RabbitMQ:Host"] ?? "localhost",
            UserName = _configuration["RabbitMQ:Username"] ?? "guest",
            Password = _configuration["RabbitMQ:Password"] ?? "guest"
        };

        using var connection = await factory.CreateConnectionAsync();
        using var channel = await connection.CreateChannelAsync();

        await channel.QueueDeclareAsync(
            queue: "order-processed-queue",
            durable: true,
            exclusive: false,
            autoDelete: false,
            arguments: null
        );

        var jsonMessage = JsonSerializer.Serialize(@event);
        var body = Encoding.UTF8.GetBytes(jsonMessage);

        var properties = new BasicProperties { Persistent = true };

        await channel.BasicPublishAsync(
            exchange: string.Empty,
            routingKey: "order-processed-queue",
            mandatory: true,
            basicProperties: properties,
            body: body
        );

        _logger.LogInformation("Evento OrderProcessedIntegrationEvent publicado a RabbitMQ para la orden {OrderId}", @event.OrderId);
    }
}
