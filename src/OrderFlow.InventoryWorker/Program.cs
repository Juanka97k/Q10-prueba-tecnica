using Microsoft.EntityFrameworkCore;
using OrderFlow.Infrastructure.Persistence;
using OrderFlow.InventoryWorker;
using OrderFlow.InventoryWorker.Services;
using OrderFlow.Shared.Configuration;

// 1. Cargar archivo .env local si existe
EnvLoader.Load(Directory.GetCurrentDirectory());

var builder = Host.CreateApplicationBuilder(args);
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
    throw new InvalidOperationException("FALLA CRÍTICA DE CONFIGURACIÓN EN WORKER: No se encontró la cadena de conexión 'DefaultConnection' ni las variables de entorno de PostgreSQL (POSTGRES_HOST, POSTGRES_DB, POSTGRES_USER, POSTGRES_PASSWORD).");
}

// 3. Validar variables requeridas de RabbitMQ (Falla rápida si falta)
var rabbitHost = builder.Configuration["RabbitMQ:Host"] ?? builder.Configuration["RABBITMQ_HOST"];
var rabbitUser = builder.Configuration["RabbitMQ:Username"] ?? builder.Configuration["RABBITMQ_USER"];
var rabbitPass = builder.Configuration["RabbitMQ:Password"] ?? builder.Configuration["RABBITMQ_PASSWORD"];

if (string.IsNullOrWhiteSpace(rabbitHost) || string.IsNullOrWhiteSpace(rabbitUser) || string.IsNullOrWhiteSpace(rabbitPass))
{
    throw new InvalidOperationException("FALLA CRÍTICA DE CONFIGURACIÓN EN WORKER: Faltan variables de entorno requeridas para RabbitMQ (RABBITMQ_HOST, RABBITMQ_USER, RABBITMQ_PASSWORD).");
}

// 4. Registrar Servicios
builder.Services.AddDbContext<OrderFlowDbContext>(options =>
    options.UseNpgsql(connectionString));

builder.Services.AddScoped<IInventoryProcessor, InventoryProcessor>();
builder.Services.AddHostedService<Worker>();

var host = builder.Build();
host.Run();