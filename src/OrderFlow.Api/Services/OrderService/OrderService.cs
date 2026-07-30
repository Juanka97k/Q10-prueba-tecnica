using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using OrderFlow.Api.DTOs;
using OrderFlow.Infrastructure.Entities;
using OrderFlow.Infrastructure.Persistence;
using OrderFlow.Shared.Events;

namespace OrderFlow.Api.Services;

public class OrderService : IOrderService
{
    private readonly OrderFlowDbContext _context;
    private readonly IMessagePublisher _publisher;
    private readonly ILogger<OrderService> _logger;

    public OrderService(
        OrderFlowDbContext context, 
        IMessagePublisher publisher,
        ILogger<OrderService> logger)
    {
        _context = context;
        _publisher = publisher;
        _logger = logger;
    }

    public async Task<OrderResponse> CreateOrderAsync(CreateOrderRequest request, CancellationToken cancellationToken = default)
    {
        try
        {
            var pedido = new Pedido
            {
                Id = Guid.NewGuid(),
                ClienteNombre = request.ClienteNombre,
                Sku = request.Sku,
                Cantidad = request.Cantidad,
                Estado = OrderStatus.Pending,
                CreadoEn = DateTime.UtcNow
            };

            _context.Pedidos.Add(pedido);
            await _context.SaveChangesAsync(cancellationToken);

            var integrationEvent = new OrderCreatedIntegrationEvent(
                EventId: Guid.NewGuid(),
                OrderId: pedido.Id,
                Sku: pedido.Sku,
                Cantidad: pedido.Cantidad,
                OcurridoEn: DateTime.UtcNow
            );

            await _publisher.PublishOrderCreatedAsync(integrationEvent);

            _logger.LogInformation("Pedido {OrderId} creado exitosamente y evento publicado a RabbitMQ.", pedido.Id);

            return new OrderResponse(
                pedido.Id,
                pedido.ClienteNombre,
                pedido.Sku,
                pedido.Cantidad,
                pedido.Estado,
                pedido.CreadoEn
            );
        }
        catch (DbUpdateException ex)
        {
            _logger.LogError(ex, "Error de base de datos al intentar guardar el pedido del cliente {ClienteNombre}.", request.ClienteNombre);
            throw new Exception("Error de persistencia al intentar registrar el pedido en la base de datos.", ex);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error inesperado al crear el pedido para el cliente {ClienteNombre}.", request.ClienteNombre);
            throw;
        }
    }

    public async Task<IEnumerable<OrderResponse>> GetOrdersAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            return await _context.Pedidos
                .OrderByDescending(p => p.CreadoEn)
                .Select(p => new OrderResponse(p.Id, p.ClienteNombre, p.Sku, p.Cantidad, p.Estado, p.CreadoEn))
                .ToListAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error de base de datos al obtener el listado de pedidos.");
            throw new Exception("No se pudo recuperar el listado de pedidos desde la base de datos.", ex);
        }
    }

    public async Task<OrderResponse?> GetOrderByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        try
        {
            var pedido = await _context.Pedidos.FindAsync(new object[] { id }, cancellationToken);

            if (pedido == null)
            {
                _logger.LogWarning("No se encontró ningún pedido registrado con ID {OrderId}.", id);
                return null;
            }

            return new OrderResponse(
                pedido.Id,
                pedido.ClienteNombre,
                pedido.Sku,
                pedido.Cantidad,
                pedido.Estado,
                pedido.CreadoEn
            );
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al consultar el pedido con ID {OrderId}.", id);
            throw new Exception($"Error al intentar obtener el pedido con ID '{id}'.", ex);
        }
    }

    public async Task<IEnumerable<StockResponse>> GetStocksAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            return await _context.Stocks
                .AsNoTracking()
                .Select(s => new StockResponse(s.Sku, s.Disponible))
                .ToListAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al consultar el catálogo de stocks en la base de datos.");
            throw new Exception("No se pudo consultar el catálogo de stocks desde la base de datos.", ex);
        }
    }
}
