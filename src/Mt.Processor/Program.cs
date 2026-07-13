using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Mt.Persistence;
using Mt.Portal;
using Mt.Processor;
using Mt.Source;
using Mt.Target;

var builder = Host.CreateApplicationBuilder(args);

var connectionString = builder.Configuration.GetConnectionString("Workshop")
    ?? "Host=localhost;Port=5432;Database=workshop;Username=workshop;Password=workshop";

builder.Services.AddPersistence(connectionString);
builder.Services.AddSimulatedSource(builder.Configuration);
builder.Services.AddSimulatedTarget(builder.Configuration);
builder.Services.AddTransport(builder.Configuration);
builder.Services.AddStageHandlers(builder.Configuration);
builder.Services.AddPortal();
builder.Services.AddHostedService<ProcessorWorker>();

builder.Build().Run();
