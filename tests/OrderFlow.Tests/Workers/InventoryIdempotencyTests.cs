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

public class InventoryIdempotencyTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly DbContextOptions<OrderFlowDbContext> _options;

    public InventoryIdempotencyTests()
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
    public async Task ProcesarEventoDuplicado_DebeSerIdempotenteYNoProcesarDosVeces()
    {
        // 1. Arrange (Preparación)
        using var context = new OrderFlowDbContext(_options);

        var pedidoId = Guid.NewGuid();
        var eventIdUnico = Guid.NewGuid(); // Mismo EventId para simular la re-entrega de RabbitMQ
        var sku = "ABC-01";

        // Configurar stock inicial de 50 unidades
        var stock = await context.Stocks.FindAsync(sku);
        if (stock != null)
        {
            stock.Disponible = 50;
        }

        // Crear pedido de 10 unidades en estado Pending
        context.Pedidos.Add(new Pedido
        {
            Id = pedidoId,
            ClienteNombre = "Juan Carlos",
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

        // Creamos el evento de integración con un EventId fijo
        var @event = new OrderCreatedIntegrationEvent(
            EventId: eventIdUnico,
            OrderId: pedidoId,
            Sku: sku,
            Cantidad: 10,
            OcurridoEn: DateTime.UtcNow
        );

        // 2. Act (Ejecución de la acción 2 veces)

        // Primera ejecución (Debe descontar 10 unidades del stock)
        try { await processor.ProcessOrderCreatedAsync(@event, CancellationToken.None); } catch { }

        // Segunda ejecución del MISMO evento (Debe ser ignorada por idempotencia)
        try { await processor.ProcessOrderCreatedAsync(@event, CancellationToken.None); } catch { }

        // 3. Assert (Verificación)
        using var verifyContext = new OrderFlowDbContext(_options);
        var pedidoActualizado = await verifyContext.Pedidos.FindAsync(pedidoId);
        var stockActualizado = await verifyContext.Stocks.FindAsync(sku);
        var eventoRegistrado = await verifyContext.ProcessedEvents.FindAsync(eventIdUnico);

        // A. Verificar que el evento se registró en la tabla Inbox (ProcessedEvents)
        Assert.NotNull(eventoRegistrado);

        // B. Verificar que el estado del pedido pasó a Confirmed
        Assert.NotNull(pedidoActualizado);
        Assert.Equal(OrderStatus.Confirmed, pedidoActualizado.Estado);

        // C. VERIFICACIÓN CLAVE: El stock solo se descontó 1 vez (50 - 10 = 40)
        // Si no fuera idempotente, habría restado dos veces dejando el stock en 30.
        Assert.NotNull(stockActualizado);
        Assert.Equal(40, stockActualizado.Disponible);
    }
}
