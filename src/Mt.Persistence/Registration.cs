using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Mt.Persistence.Outboxes;
using Mt.Transport;
using Mt.Transport.InMemory;

namespace Mt.Persistence;

/// <summary>Registers the EF context and every persistence-side port implementation (§3).</summary>
public static class Registration
{
    /// <summary>
    /// Binds transport configuration and wires the selected implementation behind the
    /// publish/consume ports (§8.3). Lives here rather than in <c>Mt.Transport</c> because the
    /// Postgres implementation is a persistence adapter (spec 12 D6) — the domain, outbox and
    /// inbox code is identical regardless of the choice.
    /// </summary>
    public static IServiceCollection AddTransport(this IServiceCollection services, IConfiguration configuration)
    {
        var section = configuration.GetSection("Transport");
        services.Configure<TransportSettings>(section);
        var kind = section.GetValue<string>("Kind") ?? "InMemory";

        if (string.Equals(kind, "Postgres", StringComparison.OrdinalIgnoreCase))
        {
            services.AddScoped<IMessagePublisher, Transport.PostgresPublisher>();
            services.AddSingleton<IMessageConsumer, Transport.PostgresConsumer>();
        }
        else
        {
            services.AddSingleton<InMemoryBus>();
            services.AddSingleton<IMessagePublisher, InMemoryPublisher>();
            services.AddSingleton<IMessageConsumer, InMemoryConsumer>();
        }

        return services;
    }

    public static IServiceCollection AddPersistence(this IServiceCollection services, string connectionString)
    {
        services.AddDbContext<WorkshopDbContext>(options => options.UseNpgsql(connectionString));

        // Migration lifecycle
        services.AddScoped<Mt.Domain.Migrations.ISetCompleted, Migrations.Sets.SetCompleted>();
        services.AddScoped<Mt.Domain.Migrations.ISetCancelled, Migrations.Sets.SetCancelled>();

        // Per-stage fetch adapters (spec 9)
        services.AddScoped<Mt.Domain.Stages.LocksSource.IFetchMigration, Stages.LocksSource.FetchMigration>();
        services.AddScoped<Mt.Domain.Stages.LocksTarget.IFetchMigration, Stages.LocksTarget.FetchMigration>();
        services.AddScoped<Mt.Domain.Stages.TriggersExport.IFetchMigration, Stages.TriggersExport.FetchMigration>();
        services.AddScoped<Mt.Domain.Stages.Transforms.IFetchMigration, Stages.Transforms.FetchMigration>();
        services.AddScoped<Mt.Domain.Stages.UploadsPreSaft.IFetchMigration, Stages.UploadsPreSaft.FetchMigration>();
        services.AddScoped<Mt.Domain.Stages.UploadsSaft.IFetchMigration, Stages.UploadsSaft.FetchMigration>();
        services.AddScoped<Mt.Domain.Stages.UnlocksSource.IFetchMigration, Stages.UnlocksSource.FetchMigration>();
        services.AddScoped<Mt.Domain.Stages.UnlocksTarget.IFetchMigration, Stages.UnlocksTarget.FetchMigration>();

        // Retryable stage flags
        services.AddScoped<Mt.Domain.Stages.LocksSource.ISetSourceLocked, Stages.LocksSource.SetSourceLocked>();
        services.AddScoped<Mt.Domain.Stages.LocksTarget.ISetTargetLocked, Stages.LocksTarget.SetTargetLocked>();
        services.AddScoped<Mt.Domain.Stages.TriggersExport.ISetExportTriggered, Stages.TriggersExport.SetExportTriggered>();
        services.AddScoped<Mt.Domain.Stages.UnlocksSource.ISetSourceUnlocked, Stages.UnlocksSource.SetSourceUnlocked>();
        services.AddScoped<Mt.Domain.Stages.UnlocksTarget.ISetTargetUnlocked, Stages.UnlocksTarget.SetTargetUnlocked>();

        // Linear state transitions
        services.AddScoped<Mt.Domain.Stages.Transforms.ISetTransformed, Stages.Transforms.SetTransformed>();
        services.AddScoped<Mt.Domain.Stages.Transforms.ISetCancelling, Stages.Transforms.SetCancelling>();
        services.AddScoped<Mt.Domain.Stages.UploadsPreSaft.ISetPreSaftUploaded, Stages.UploadsPreSaft.SetPreSaftUploaded>();
        services.AddScoped<Mt.Domain.Stages.UploadsSaft.ISetSaftUploaded, Stages.UploadsSaft.SetSaftUploaded>();

        // Cross-cutting
        services.AddScoped<Mt.Domain.IAdd, Outboxes.Add>();
        services.AddScoped<Mt.Domain.IScheduleEvent, ScheduleEvent>();

        // External ids (spec 8)
        services.AddScoped<Mt.Domain.ExternalIds.IAdd, ExternalIds.Add>();
        services.AddScoped<Mt.Domain.ExternalIds.IFetch, ExternalIds.Fetch>();
        services.AddScoped<Mt.Domain.ExternalIds.ICancel, ExternalIds.Cancel>();

        // API commands
        services.AddScoped<Mt.Domain.Commands.IStart, Commands.Start>();
        services.AddScoped<Mt.Domain.Commands.IApprove, Commands.Approve>();
        services.AddScoped<Mt.Domain.Commands.ICancel, Commands.Cancel>();

        // Infrastructure services
        services.AddScoped<ExecuteOnce>();
        services.AddScoped<OutboxStore>();
        services.AddScoped<Scheduler>();

        return services;
    }
}
