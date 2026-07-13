using Microsoft.EntityFrameworkCore;
using Mt.Api;
using Mt.Outbox;
using Mt.Persistence;
using Mt.Portal;
using Mt.Processor;
using Mt.Source;
using Mt.Target;

var builder = WebApplication.CreateBuilder(args);

var connectionString = builder.Configuration.GetConnectionString("Workshop")
    ?? "Host=localhost;Port=5432;Database=workshop;Username=workshop;Password=workshop";

builder.Services.AddPersistence(connectionString);
builder.Services.AddSimulatedSource(builder.Configuration);
builder.Services.AddSimulatedTarget(builder.Configuration);
builder.Services.AddTransport(builder.Configuration);
builder.Services.AddStageHandlers(builder.Configuration);
builder.Services.AddPortal();
builder.Services.AddHostedService<OutboxWorker>();
builder.Services.AddHostedService<ProcessorWorker>();

var app = builder.Build();

// Apply migrations on startup so `docker compose up` + `dotnet run` is a single step (§13).
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<WorkshopDbContext>();
    await db.Database.MigrateAsync();
}

app.MapMigrationEndpoints();
app.Run();
