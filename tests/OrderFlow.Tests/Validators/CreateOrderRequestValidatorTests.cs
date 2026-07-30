using Microsoft.EntityFrameworkCore;
using OrderFlow.Api.DTOs;
using OrderFlow.Api.Validators;
using OrderFlow.Infrastructure.Entities;
using OrderFlow.Infrastructure.Persistence;
using Xunit;

namespace OrderFlow.Tests.Validators;

public class CreateOrderRequestValidatorTests
{
    private OrderFlowDbContext GetInMemoryDbContext()
    {
        var options = new DbContextOptionsBuilder<OrderFlowDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        var context = new OrderFlowDbContext(options);
        
        // Agregar stock inicial de prueba
        context.Stocks.Add(new Stock { Sku = "ABC-01", Disponible = 50 });
        context.SaveChanges();

        return context;
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-5)]
    [InlineData(101)]
    public async Task Validar_CantidadFueraDeRango_DebeRetornarErrorDeValidacion(int cantidadInvalida)
    {
        // 1. Arrange (Preparación)
        using var context = GetInMemoryDbContext();
        var validator = new CreateOrderRequestValidator(context);

        var request = new CreateOrderRequest(
            ClienteNombre: "Juan Pérez",
            Sku: "ABC-01",
            Cantidad: cantidadInvalida
        );

        // 2. Act (Ejecución)
        var result = await validator.ValidateAsync(request);

        // 3. Assert (Verificación)
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.PropertyName == nameof(CreateOrderRequest.Cantidad));
    }

    [Fact]
    public async Task Validar_SkuInexistente_DebeRetornarErrorDeValidacion()
    {
        // 1. Arrange (Preparación)
        using var context = GetInMemoryDbContext();
        var validator = new CreateOrderRequestValidator(context);

        var request = new CreateOrderRequest(
            ClienteNombre: "Juan Pérez",
            Sku: "SKU-INEXISTENTE",
            Cantidad: 5
        );

        // 2. Act (Ejecución)
        var result = await validator.ValidateAsync(request);

        // 3. Assert (Verificación)
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.PropertyName == nameof(CreateOrderRequest.Sku));
    }
}
