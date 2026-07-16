using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Mt.Domain.Steps;

namespace Mt.Processor;

/// <summary>
/// Registers the step handlers and their per-step retry settings (§6.5). Handlers are registered
/// as <see cref="IHandleDomainEvent"/> so the inbox can resolve the right one by event type.
/// </summary>
public static class StepHandlers
{
    public static IServiceCollection AddStepHandlers(this IServiceCollection services, IConfiguration configuration)
    {
        // Per-step retry budgets. Keys match the slice names so they are greppable (spec 12
        // D10); defaults apply when a section is absent.
        services.AddSingleton(configuration.GetSection("Steps:LocksSource").Get<Domain.Steps.LocksSource.Settings>() ?? new());
        services.AddSingleton(configuration.GetSection("Steps:LocksTarget").Get<Domain.Steps.LocksTarget.Settings>() ?? new());
        services.AddSingleton(configuration.GetSection("Steps:TriggersExport").Get<Domain.Steps.TriggersExport.Settings>() ?? new());
        services.AddSingleton(configuration.GetSection("Steps:UnlocksSource").Get<Domain.Steps.UnlocksSource.Settings>() ?? new());
        services.AddSingleton(configuration.GetSection("Steps:UnlocksTarget").Get<Domain.Steps.UnlocksTarget.Settings>() ?? new());

        services.AddScoped<IHandleDomainEvent, Domain.Steps.LocksSource.Handler>();
        services.AddScoped<IHandleDomainEvent, Domain.Steps.LocksTarget.Handler>();
        services.AddScoped<IHandleDomainEvent, Domain.Steps.TriggersExport.Handler>();
        services.AddScoped<IHandleDomainEvent, Domain.Steps.Transforms.Handler>();
        services.AddScoped<IHandleDomainEvent, Domain.Steps.UploadsPreSaft.Handler>();
        services.AddScoped<IHandleDomainEvent, Domain.Steps.UploadsSaft.Handler>();
        services.AddScoped<IHandleDomainEvent, Domain.Steps.UnlocksSource.Handler>();
        services.AddScoped<IHandleDomainEvent, Domain.Steps.UnlocksTarget.Handler>();

        return services;
    }
}
