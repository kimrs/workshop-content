using Microsoft.Extensions.DependencyInjection;
using Mt.Domain;

namespace Mt.Portal;

/// <summary>Registers the portal-side implementation of the completion notification port.</summary>
public static class Registration
{
    public static IServiceCollection AddPortal(this IServiceCollection services)
    {
        services.AddScoped<INotifyCompletion, NotifyCompletion>();

        return services;
    }
}
