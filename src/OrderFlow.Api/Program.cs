using FluentValidation;
using Microsoft.EntityFrameworkCore;
using OrderFlow.Api.Services;
using OrderFlow.Api.Validators;
using OrderFlow.Infrastructure.Persistence;

var builder = WebApplication.CreateBuilder(args);

// 1. Agregar Controladores
builder.Services.AddControllers();

// 2. Configurar OpenAPI / Swagger
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddOpenApi();

// 3. Obtener Cadena de Conexión desde appsettings.json o Variables de Entorno
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");

// 4. Registrar Servicios del Dominio e Infraestructura
builder.Services.AddScoped<IMessagePublisher, RabbitMQPublisher>();
builder.Services.AddScoped<IOrderService, OrderService>();
builder.Services.AddValidatorsFromAssemblyContaining<CreateOrderRequestValidator>();

// 5. Registrar Entity Framework Core con PostgreSQL
builder.Services.AddDbContext<OrderFlowDbContext>(options =>
    options.UseNpgsql(connectionString));

var app = builder.Build();

// 6. Mapear Swagger / OpenAPI en Desarrollo
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();

// 7. Registrar Controladores de la API
app.MapControllers();

// 8. Ejecutar Migraciones y Seeding Automático al Arranque de la App
using (var scope = app.Services.CreateScope())
{
    var dbContext = scope.ServiceProvider.GetRequiredService<OrderFlowDbContext>();

    // Aplica migraciones pendientes y crea las tablas si no existen
    await dbContext.Database.MigrateAsync();
}

app.Run();