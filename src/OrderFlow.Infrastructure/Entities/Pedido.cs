namespace OrderFlow.Infrastructure.Entities;

public class Pedido
{
    public Guid Id { get; set; }
    public string ClienteNombre { get; set; } = string.Empty;
    public string Sku { get; set; } = string.Empty;
    public int Cantidad { get; set; }
    public OrderStatus Estado { get; set; } = OrderStatus.Pending;
    public DateTime CreadoEn { get; set; } = DateTime.UtcNow;
}
