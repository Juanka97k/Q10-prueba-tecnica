namespace OrderFlow.Api.DTOs;

public record CreateOrderRequest(
    string ClienteNombre,
    string Sku,
    int Cantidad
);
