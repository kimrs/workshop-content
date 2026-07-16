using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Mt.Outbox;
using Mt.Persistence;

var builder = Host.CreateApplicationBuilder(args);

var connectionString = builder.Configuration.GetConnectionString("Workshop")
    ?? "Host=localhost;Port=5432;Database=workshop;Username=workshop;Password=workshop";

builder.Services.AddPersistence(connectionString);
builder.Services.AddTransport(builder.Configuration);
builder.Services.AddHostedService<OutboxWorker>();

builder.Build().Run();
