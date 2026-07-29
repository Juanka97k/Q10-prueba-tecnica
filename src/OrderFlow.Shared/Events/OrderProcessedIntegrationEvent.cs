namespace OrderFlow.Shared.Events;

public record OrderProcessedIntegrationEvent(
    Guid OrderId,
    string Estado, // "Confirmed" o "Rejected"
    DateTime ProcesadoEn
);
