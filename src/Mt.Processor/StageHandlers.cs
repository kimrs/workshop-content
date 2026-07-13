using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Mt.Domain.Stages;

namespace Mt.Processor;

/// <summary>
/// Registers the stage handlers and their per-stage retry settings (§6.5). Handlers are registered
/// as <see cref="IHandleDomainEvent"/> so the inbox can resolve the right one by event type.
/// </summary>
public static class StageHandlers
{
    public static IServiceCollection AddStageHandlers(this IServiceCollection services, IConfiguration configuration)
    {
        // Per-stage retry budgets. Keys match the slice names so they are greppable (spec 12
        // D10); defaults apply when a section is absent.
        services.AddSingleton(configuration.GetSection("Stages:LocksSource").Get<Domain.Stages.LocksSource.Settings>() ?? new());
        services.AddSingleton(configuration.GetSection("Stages:LocksTarget").Get<Domain.Stages.LocksTarget.Settings>() ?? new());
        services.AddSingleton(configuration.GetSection("Stages:TriggersExport").Get<Domain.Stages.TriggersExport.Settings>() ?? new());
        services.AddSingleton(configuration.GetSection("Stages:UnlocksSource").Get<Domain.Stages.UnlocksSource.Settings>() ?? new());
        services.AddSingleton(configuration.GetSection("Stages:UnlocksTarget").Get<Domain.Stages.UnlocksTarget.Settings>() ?? new());

        services.AddScoped<IHandleDomainEvent, Domain.Stages.LocksSource.Handler>();
        services.AddScoped<IHandleDomainEvent, Domain.Stages.LocksTarget.Handler>();
        services.AddScoped<IHandleDomainEvent, Domain.Stages.TriggersExport.Handler>();
        services.AddScoped<IHandleDomainEvent, Domain.Stages.Transforms.Handler>();
        services.AddScoped<IHandleDomainEvent, Domain.Stages.UploadsPreSaft.Handler>();
        services.AddScoped<IHandleDomainEvent, Domain.Stages.UploadsSaft.Handler>();
        services.AddScoped<IHandleDomainEvent, Domain.Stages.UnlocksSource.Handler>();
        services.AddScoped<IHandleDomainEvent, Domain.Stages.UnlocksTarget.Handler>();

        return services;
    }
}
