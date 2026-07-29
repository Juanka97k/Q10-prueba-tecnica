using Microsoft.EntityFrameworkCore;
using OrderFlow.Infrastructure.Entities;

namespace OrderFlow.Infrastructure.Persistence;

public class OrderFlowDbContext : DbContext
{
    public OrderFlowDbContext(DbContextOptions<OrderFlowDbContext> options) 
        : base(options)
    {
    }

    public DbSet<Pedido> Pedidos => Set<Pedido>();
    public DbSet<Stock> Stocks => Set<Stock>();
    public DbSet<ProcessedEvent> ProcessedEvents => Set<ProcessedEvent>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // 1. Configuración de Pedido
        modelBuilder.Entity<Pedido>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.ClienteNombre).IsRequired().HasMaxLength(100);
            entity.Property(e => e.Sku).IsRequired().HasMaxLength(50);
            entity.Property(e => e.Estado).HasConversion<string>(); // Guarda el Enum como String en Postgres ("Pending", etc.)
        });

        // 2. Configuración de Stock
        modelBuilder.Entity<Stock>(entity =>
        {
            entity.HasKey(e => e.Sku); // Sku es la Clave Primaria
            entity.Property(e => e.Sku).HasMaxLength(50);

            // Seed inicial de productos como exige la prueba
            entity.HasData(
                new Stock { Sku = "ABC-01", Disponible = 100 },
                new Stock { Sku = "XYZ-02", Disponible = 50 },
                new Stock { Sku = "LMN-03", Disponible = 10 }
            );
        });

        // 3. Configuración de ProcessedEvent (Idempotencia)
        modelBuilder.Entity<ProcessedEvent>(entity =>
        {
            entity.HasKey(e => e.EventId);
        });
    }
}
