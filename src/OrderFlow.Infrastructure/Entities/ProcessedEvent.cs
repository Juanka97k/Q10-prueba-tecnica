namespace OrderFlow.Infrastructure.Entities;

public class ProcessedEvent
{
    // El EventId del mensaje de RabbitMQ será la clave primaria
    public Guid EventId { get; set; }
    public DateTime ProcesadoEn { get; set; } = DateTime.UtcNow;
}
