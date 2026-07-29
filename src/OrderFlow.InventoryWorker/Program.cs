using Microsoft.EntityFrameworkCore;
using OrderFlow.Infrastructure.Persistence;
using OrderFlow.InventoryWorker;
using OrderFlow.InventoryWorker.Services; // 👈 Asegúrate de incluir este namespace

var builder = Host.CreateApplicationBuilder(args);

// 1. Registrar DbContext
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
builder.Services.AddDbContext<OrderFlowDbContext>(options =>
    options.UseNpgsql(connectionString));

// 2. REGISTRAR EL PROCESADOR (Esta es la línea que falta)
builder.Services.AddScoped<IInventoryProcessor, InventoryProcessor>();

// 3. Registrar el BackgroundService
builder.Services.AddHostedService<Worker>();

var host = builder.Build();
host.Run();