using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using OrderFlow.Api.DTOs;
using OrderFlow.Api.Services;
using OrderFlow.Infrastructure.Entities;
using OrderFlow.Infrastructure.Persistence;
using OrderFlow.Shared.Events;
using Xunit;

namespace OrderFlow.Tests.Services;

public class OrderServiceTests
{
    private OrderFlowDbContext GetInMemoryDbContext()
    {
        var options = new DbContextOptionsBuilder<OrderFlowDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        return new OrderFlowDbContext(options);
    }

    [Fact]
    public async Task CrearPedido_SolicitudValida_DebeGuardarPedidoPendienteYPublicarEvento()
    {
        // 1. Arrange (Preparación)
        using var context = GetInMemoryDbContext();
        var mockPublisher = new Mock<IMessagePublisher>();
        var mockLogger = new Mock<ILogger<OrderService>>();

        var service = new OrderService(context, mockPublisher.Object, mockLogger.Object);

        var request = new CreateOrderRequest(
            ClienteNombre: "Maria Gómez",
            Sku: "ABC-01",
            Cantidad: 10
        );

        // 2. Act (Ejecución)
        var response = await service.CreateOrderAsync(request);

        // 3. Assert (Verificación)
        Assert.NotNull(response);
        Assert.NotEqual(Guid.Empty, response.Id);
        Assert.Equal("Maria Gómez", response.ClienteNombre);
        Assert.Equal(OrderStatus.Pending, response.Estado);

        // Verificar que la orden se guardó efectivamente en la BD InMemory
        var orderInDb = await context.Pedidos.FindAsync(response.Id);
        Assert.NotNull(orderInDb);
        Assert.Equal(OrderStatus.Pending, orderInDb.Estado);

        // Verificar que el publisher de RabbitMQ fue invocado exactamente 1 vez
        mockPublisher.Verify(
            p => p.PublishOrderCreatedAsync(It.IsAny<OrderCreatedIntegrationEvent>()),
            Times.Once
        );
    }
}
