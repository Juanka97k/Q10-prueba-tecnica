using OrderFlow.Api.DTOs;

namespace OrderFlow.Api.Services;

public interface IOrderService
{
    Task<OrderResponse> CreateOrderAsync(CreateOrderRequest request, CancellationToken cancellationToken = default);
    Task<IEnumerable<OrderResponse>> GetOrdersAsync(CancellationToken cancellationToken = default);
    Task<OrderResponse?> GetOrderByIdAsync(Guid id, CancellationToken cancellationToken = default);
}
