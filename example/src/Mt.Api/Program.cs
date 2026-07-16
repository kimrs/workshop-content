using Mt.Api;
using Mt.Persistence;
using Mt.Portal;

var builder = WebApplication.CreateBuilder(args);

var connectionString = builder.Configuration.GetConnectionString("Workshop")
    ?? "Host=localhost;Port=5432;Database=workshop;Username=workshop;Password=workshop";

builder.Services.AddPersistence(connectionString);
// The Cancel command's immediate-finalize path notifies completion (spec 12 D1).
builder.Services.AddPortal();

var app = builder.Build();
app.MapMigrationEndpoints();
app.Run();
