using OrderFlow.Shared.Events;

namespace OrderFlow.Api.Services;

public interface IMessagePublisher
{
    Task PublishOrderCreatedAsync(OrderCreatedIntegrationEvent @event);
}
