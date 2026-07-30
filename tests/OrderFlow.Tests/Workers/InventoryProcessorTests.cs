using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Moq;
using OrderFlow.Infrastructure.Entities;
using OrderFlow.Infrastructure.Persistence;
using OrderFlow.InventoryWorker.Services;
using OrderFlow.Shared.Events;
using Xunit;

namespace OrderFlow.Tests.Workers;

public class InventoryProcessorTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly DbContextOptions<OrderFlowDbContext> _options;

    public InventoryProcessorTests()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();

        _options = new DbContextOptionsBuilder<OrderFlowDbContext>()
            .UseSqlite(_connection)
            .Options;

        using var context = new OrderFlowDbContext(_options);
        context.Database.EnsureCreated();
    }

    public void Dispose()
    {
        _connection.Dispose();
    }

    [Fact]
    public async Task ProcesarPedido_StockSuficiente_DebeConfirmarPedidoYDescontarStock()
    {
        // 1. Arrange (Preparación)
        using var context = new OrderFlowDbContext(_options);
        
        var pedidoId = Guid.NewGuid();
        var sku = "ABC-01";
        
        var stock = await context.Stocks.FindAsync(sku);
        if (stock != null)
        {
            stock.Disponible = 50;
        }

        context.Pedidos.Add(new Pedido
        {
            Id = pedidoId,
            ClienteNombre = "Carlos Ruiz",
            Sku = sku,
            Cantidad = 10,
            Estado = OrderStatus.Pending,
            CreadoEn = DateTime.UtcNow
        });
        await context.SaveChangesAsync();

        var mockConfig = new Mock<IConfiguration>();
        mockConfig.Setup(c => c["RabbitMQ:Host"]).Returns("invalid_host_for_test");
        var mockLogger = new Mock<ILogger<InventoryProcessor>>();

        var processor = new InventoryProcessor(context, mockConfig.Object, mockLogger.Object);

        var @event = new OrderCreatedIntegrationEvent(
            EventId: Guid.NewGuid(),
            OrderId: pedidoId,
            Sku: sku,
            Cantidad: 10,
            OcurridoEn: DateTime.UtcNow
        );

        // 2. Act (Ejecución)
        try
        {
            await processor.ProcessOrderCreatedAsync(@event, CancellationToken.None);
        }
        catch
        {
            // Se ignora la falta de conexión a RabbitMQ físico durante la prueba unitaria
        }

        // 3. Assert (Verificación)
        using var verifyContext = new OrderFlowDbContext(_options);
        var pedidoActualizado = await verifyContext.Pedidos.FindAsync(pedidoId);
        var stockActualizado = await verifyContext.Stocks.FindAsync(sku);

        Assert.NotNull(pedidoActualizado);
        Assert.Equal(OrderStatus.Confirmed, pedidoActualizado.Estado);
        Assert.NotNull(stockActualizado);
        Assert.Equal(40, stockActualizado.Disponible); // 50 - 10 = 40
    }

    [Fact]
    public async Task ProcesarPedido_StockInsuficiente_DebeRechazarPedidoSinDescontarStock()
    {
        // 1. Arrange (Preparación)
        using var context = new OrderFlowDbContext(_options);
        
        var pedidoId = Guid.NewGuid();
        var sku = "XYZ-02";
        
        var stock = await context.Stocks.FindAsync(sku);
        if (stock != null)
        {
            stock.Disponible = 5; // Solo 5 unidades disponibles
        }

        context.Pedidos.Add(new Pedido
        {
            Id = pedidoId,
            ClienteNombre = "Ana Lopez",
            Sku = sku,
            Cantidad = 20, // Solicita 20 (Insuficiente)
            Estado = OrderStatus.Pending,
            CreadoEn = DateTime.UtcNow
        });
        await context.SaveChangesAsync();

        var mockConfig = new Mock<IConfiguration>();
        mockConfig.Setup(c => c["RabbitMQ:Host"]).Returns("invalid_host_for_test");
        var mockLogger = new Mock<ILogger<InventoryProcessor>>();

        var processor = new InventoryProcessor(context, mockConfig.Object, mockLogger.Object);

        var @event = new OrderCreatedIntegrationEvent(
            EventId: Guid.NewGuid(),
            OrderId: pedidoId,
            Sku: sku,
            Cantidad: 20,
            OcurridoEn: DateTime.UtcNow
        );

        // 2. Act (Ejecución)
        try
        {
            await processor.ProcessOrderCreatedAsync(@event, CancellationToken.None);
        }
        catch
        {
            // Se ignora la falta de conexión a RabbitMQ físico durante la prueba unitaria
        }

        // 3. Assert (Verificación)
        using var verifyContext = new OrderFlowDbContext(_options);
        var pedidoActualizado = await verifyContext.Pedidos.FindAsync(pedidoId);
        var stockActualizado = await verifyContext.Stocks.FindAsync(sku);

        Assert.NotNull(pedidoActualizado);
        Assert.Equal(OrderStatus.Rejected, pedidoActualizado.Estado);
        Assert.NotNull(stockActualizado);
        Assert.Equal(5, stockActualizado.Disponible); // Mantiene 5 intactos
    }
}
