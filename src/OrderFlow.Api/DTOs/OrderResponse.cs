using OrderFlow.Infrastructure.Entities;

namespace OrderFlow.Api.DTOs;

public record OrderResponse(
    Guid Id,
    string ClienteNombre,
    string Sku,
    int Cantidad,
    OrderStatus Estado,
    DateTime CreadoEn
);
