using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Mt.Domain.Steps.LocksSource;
using Mt.Domain.Steps.Transforms;
using Mt.Domain.Steps.TriggersExport;
using Mt.Domain.Steps.UnlocksSource;
using Mt.Domain.Steps.UploadsSaft;

namespace Mt.DistinctComics;

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
