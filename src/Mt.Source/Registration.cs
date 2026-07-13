using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Mt.Domain.Stages.LocksSource;
using Mt.Domain.Stages.Transforms;
using Mt.Domain.Stages.TriggersExport;
using Mt.Domain.Stages.UnlocksSource;
using Mt.Domain.Stages.UploadsSaft;

namespace Mt.Source;

/// <summary>Binds the simulated Source configuration and registers its port implementations (§3, §7).</summary>
public static class Registration
{
    public static IServiceCollection AddSimulatedSource(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<SourceSettings>(configuration.GetSection("Source"));
        services.Configure<ClientSettings>(configuration.GetSection("Client"));

        services.AddSingleton<Simulator>();
        services.AddScoped<ILockSource, LockSource>();
        services.AddScoped<IUnlockSource, UnlockSource>();
        services.AddScoped<ITriggerExport, TriggerExport>();
        services.AddScoped<IFetchClient, FetchClient>();
        services.AddScoped<IDownloadSaft, DownloadSaft>();

        return services;
    }
}
