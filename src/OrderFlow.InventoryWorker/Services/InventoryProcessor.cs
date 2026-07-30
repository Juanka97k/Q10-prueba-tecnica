using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using OrderFlow.Infrastructure.Entities;
using OrderFlow.Infrastructure.Persistence;
using OrderFlow.Shared.Events;
using RabbitMQ.Client;

namespace OrderFlow.InventoryWorker.Services;

/// <summary>
/// Contrato para el procesador asíncrono de inventario en el Worker.
/// </summary>
public interface IInventoryProcessor
{
    /// <summary>
    /// Procesa la creación de un pedido evaluando existencias en PostgreSQL y garantizando Idempotencia.
    /// </summary>
    Task ProcessOrderCreatedAsync(OrderCreatedIntegrationEvent integrationEvent, CancellationToken cancellationToken);
}

/// <summary>
/// Servicio procesador de inventario con gestión de transacciones PostgreSQL, deduplicación y mensajería RabbitMQ.
/// </summary>
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

    /// <summary>
    /// Procesa un evento de creación de pedido asíncronamente desde la cola 'order-created-queue'.
    /// Valida idempotencia, abre transacción atómica, descuenta stock si está disponible y notifica el resultado.
    /// </summary>
    public async Task ProcessOrderCreatedAsync(OrderCreatedIntegrationEvent integrationEvent, CancellationToken cancellationToken)
    {
        // 1. Validación de Idempotencia: Verificar si el evento ya fue procesado anteriormente
        var yaProcesado = await _context.ProcessedEvents
            .AnyAsync(p => p.EventId == integrationEvent.EventId, cancellationToken);

        if (yaProcesado)
        {
            _logger.LogWarning("Evento {EventId} ya fue procesado anteriormente. Se omite para garantizar Idempotencia.", integrationEvent.EventId);
            return;
        }

        // 2. Iniciar Transacción de Base de Datos PostgreSQL
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

            // 3. Evaluar disponibilidad e impactar inventario
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

            // 4. Registrar evento en la tabla ProcessedEvents (Inbox Pattern)
            _context.ProcessedEvents.Add(new ProcessedEvent
            {
                EventId = integrationEvent.EventId,
                ProcesadoEn = DateTime.UtcNow
            });

            await _context.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);

            // 5. Publicar evento de respuesta 'OrderProcessedIntegrationEvent' a RabbitMQ
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

    /// <summary>
    /// Publica el evento de orden procesada a la cola 'order-processed-queue' de RabbitMQ para ser retransmitido vía SignalR.
    /// </summary>
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
