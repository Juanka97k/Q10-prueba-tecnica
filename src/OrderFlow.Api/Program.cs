using FluentValidation;
using Microsoft.EntityFrameworkCore;
using OrderFlow.Api.Hubs;
using OrderFlow.Api.Services;
using OrderFlow.Api.Validators;
using OrderFlow.Infrastructure.Persistence;

var builder = WebApplication.CreateBuilder(args);

// 1. Agregar Controladores y SignalR
builder.Services.AddControllers();
builder.Services.AddSignalR();

// 2. Configurar CORS para permitir la conexión desde el cliente Angular (http://localhost:4200)
builder.Services.AddCors(options =>
{
    options.AddPolicy("CorsPolicy", policy =>
    {
        policy.WithOrigins("http://localhost:4200", "https://localhost:4200")
              .AllowAnyHeader()
              .AllowAnyMethod()
              .AllowCredentials();
    });
});

// 3. Configurar OpenAPI / Swagger
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddOpenApi();

// 4. Obtener Cadena de Conexión desde appsettings.json o Variables de Entorno
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");

// 5. Registrar Servicios del Dominio e Infraestructura
builder.Services.AddScoped<IMessagePublisher, RabbitMQPublisher>();
builder.Services.AddScoped<IOrderService, OrderService>();
builder.Services.AddValidatorsFromAssemblyContaining<CreateOrderRequestValidator>();

// 6. Registrar Consumidor de RabbitMQ en Segundo Plano (Transmisor a SignalR)
builder.Services.AddHostedService<OrderProcessedConsumerService>();

// 7. Registrar Entity Framework Core con PostgreSQL
builder.Services.AddDbContext<OrderFlowDbContext>(options =>
    options.UseNpgsql(connectionString));

var app = builder.Build();

// 8. Mapear Swagger / OpenAPI en Desarrollo
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseCors("CorsPolicy");
app.UseHttpsRedirection();

// 9. Mapear Controladores y Hub de SignalR
app.MapControllers();
app.MapHub<OrdersHub>("/hubs/orders");

// 10. Ejecutar Migraciones y Seeding Automático al Arranque de la App
using (var scope = app.Services.CreateScope())
{
    var dbContext = scope.ServiceProvider.GetRequiredService<OrderFlowDbContext>();

    // Aplica migraciones pendientes y crea las tablas si no existen
    await dbContext.Database.MigrateAsync();
}

app.Run();