namespace OrderFlow.Infrastructure.Entities;

public class Stock
{
    // El SKU será la clave primaria en la base de datos
    public string Sku { get; set; } = string.Empty;
    public int Disponible { get; set; }
}
