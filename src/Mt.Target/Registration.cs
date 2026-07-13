using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Mt.Domain.Stages.LocksTarget;
using Mt.Domain.Stages.UnlocksTarget;
using Mt.Domain.Stages.UploadsPreSaft;
using Mt.Domain.Stages.UploadsSaft;

namespace Mt.Target;

/// <summary>Binds the simulated Target configuration and registers its port implementations (§3, §7).</summary>
public static class Registration
{
    public static IServiceCollection AddSimulatedTarget(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<TargetSettings>(configuration.GetSection("Target"));

        services.AddSingleton<Simulator>();
        services.AddScoped<ILockTarget, LockTarget>();
        services.AddScoped<IUnlockTarget, UnlockTarget>();
        services.AddScoped<IUploadPreSaft, UploadPreSaft>();
        services.AddScoped<IUploadSaft, UploadSaft>();

        return services;
    }
}
