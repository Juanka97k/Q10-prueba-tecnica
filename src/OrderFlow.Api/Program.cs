using FluentValidation;
using Microsoft.EntityFrameworkCore;
using OrderFlow.Api.Services;
using OrderFlow.Api.Validators;
using OrderFlow.Infrastructure.Persistence;
using OrderFlow.Shared.Configuration;

// 1. Cargar archivo .env local si existe
EnvLoader.Load(Directory.GetCurrentDirectory());

var builder = WebApplication.CreateBuilder(args);
builder.Configuration.AddEnvironmentVariables();

// 2. Cargar y Validar Cadena de Conexión a PostgreSQL (Falla rápida si falta)
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
if (string.IsNullOrWhiteSpace(connectionString))
{
    var pgHost = builder.Configuration["POSTGRES_HOST"];
    var pgPort = builder.Configuration["POSTGRES_PORT"] ?? "5432";
    var pgDb = builder.Configuration["POSTGRES_DB"];
    var pgUser = builder.Configuration["POSTGRES_USER"];
    var pgPass = builder.Configuration["POSTGRES_PASSWORD"];

    if (!string.IsNullOrWhiteSpace(pgHost) && !string.IsNullOrWhiteSpace(pgDb) && !string.IsNullOrWhiteSpace(pgUser) && !string.IsNullOrWhiteSpace(pgPass))
    {
        connectionString = $"Host={pgHost};Port={pgPort};Database={pgDb};Username={pgUser};Password={pgPass}";
    }
}

if (string.IsNullOrWhiteSpace(connectionString))
{
    throw new InvalidOperationException("FALLA CRÍTICA DE CONFIGURACIÓN EN API: No se encontró la cadena de conexión 'DefaultConnection' ni las variables de entorno de PostgreSQL (POSTGRES_HOST, POSTGRES_DB, POSTGRES_USER, POSTGRES_PASSWORD).");
}

// 3. Validar variables requeridas de RabbitMQ (Falla rápida si falta)
var rabbitHost = builder.Configuration["RabbitMQ:Host"] ?? builder.Configuration["RABBITMQ_HOST"];
var rabbitUser = builder.Configuration["RabbitMQ:Username"] ?? builder.Configuration["RABBITMQ_USER"];
var rabbitPass = builder.Configuration["RabbitMQ:Password"] ?? builder.Configuration["RABBITMQ_PASSWORD"];

if (string.IsNullOrWhiteSpace(rabbitHost) || string.IsNullOrWhiteSpace(rabbitUser) || string.IsNullOrWhiteSpace(rabbitPass))
{
    throw new InvalidOperationException("FALLA CRÍTICA DE CONFIGURACIÓN EN API: Faltan variables de entorno requeridas para RabbitMQ (RABBITMQ_HOST, RABBITMQ_USER, RABBITMQ_PASSWORD).");
}

// 4. Agregar Controladores y OpenAPI
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddOpenApi();

// 5. Registrar Servicios de Dominio e Infraestructura
builder.Services.AddScoped<IMessagePublisher, RabbitMQPublisher>();
builder.Services.AddScoped<IOrderService, OrderService>();
builder.Services.AddValidatorsFromAssemblyContaining<CreateOrderRequestValidator>();

// 6. Registrar Entity Framework Core con PostgreSQL
builder.Services.AddDbContext<OrderFlowDbContext>(options =>
    options.UseNpgsql(connectionString));

var app = builder.Build();

// 7. Mapear Swagger / OpenAPI en Desarrollo
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();
app.MapControllers();

// 8. Ejecutar Migraciones y Seeding Automático al Arranque de la App
using (var scope = app.Services.CreateScope())
{
    var dbContext = scope.ServiceProvider.GetRequiredService<OrderFlowDbContext>();
    await dbContext.Database.MigrateAsync();
}

app.Run();