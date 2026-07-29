using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using OrderFlow.Infrastructure.Entities;
using OrderFlow.Infrastructure.Persistence;
using OrderFlow.Shared.Events;

namespace OrderFlow.InventoryWorker.Services;

public interface IInventoryProcessor
{
    Task ProcessOrderCreatedAsync(OrderCreatedIntegrationEvent integrationEvent, CancellationToken cancellationToken);
}

public class InventoryProcessor : IInventoryProcessor
{
    private readonly OrderFlowDbContext _context;
    private readonly ILogger<InventoryProcessor> _logger;

    public InventoryProcessor(OrderFlowDbContext context, ILogger<InventoryProcessor> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task ProcessOrderCreatedAsync(OrderCreatedIntegrationEvent integrationEvent, CancellationToken cancellationToken)
    {
        // 1. VERIFICACIÓN DE IDEMPOTENCIA
        var yaProcesado = await _context.ProcessedEvents
            .AnyAsync(p => p.EventId == integrationEvent.EventId, cancellationToken);

        if (yaProcesado)
        {
            _logger.LogWarning("Evento {EventId} ya fue procesado anteriormente. Se omite para garantizar Idempotencia.", integrationEvent.EventId);
            return;
        }

        // Abrimos transacción explícita para asegurar consistencia atómica
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

            // 2. LÓGICA DE NEGOCIO: VALIDAR Y DESCONTAR STOCK
            if (stock != null && stock.Disponible >= integrationEvent.Cantidad)
            {
                stock.Disponible -= integrationEvent.Cantidad;
                pedido.Estado = OrderStatus.Confirmed;
                _logger.LogInformation("Stock reservado exitosamente para Pedido {OrderId}. Nuevo stock disponible de {Sku}: {Disponible}", 
                    pedido.Id, stock.Sku, stock.Disponible);
            }
            else
            {
                pedido.Estado = OrderStatus.Rejected;
                _logger.LogWarning("Stock insuficiente para Pedido {OrderId}. Requerido: {Cantidad}, Disponible: {Disponible}. Estado cambiado a Rejected.", 
                    pedido.Id, integrationEvent.Cantidad, stock?.Disponible ?? 0);
            }

            // 3. REGISTRAR EVENTO COMO PROCESADO (Garantiza Idempotencia)
            _context.ProcessedEvents.Add(new ProcessedEvent
            {
                EventId = integrationEvent.EventId,
                ProcesadoEn = DateTime.UtcNow
            });

            await _context.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync(cancellationToken);
            _logger.LogError(ex, "Error procesando el evento {EventId} para el pedido {OrderId}. Transacción revertida.", 
                integrationEvent.EventId, integrationEvent.OrderId);
            throw; // Lanza la excepción para no enviar ACK en RabbitMQ y reintentar si es necesario
        }
    }
}
