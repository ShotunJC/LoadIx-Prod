using LoadIx.Application.System;
using Microsoft.Extensions.DependencyInjection;

namespace LoadIx.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(
        this IServiceCollection services,
        SystemInfoDescriptor systemInfoDescriptor)
    {
        services.AddSingleton(TimeProvider.System);
        services.AddSingleton(systemInfoDescriptor);
        services.AddSingleton<ISystemInfoQueryHandler, SystemInfoQueryHandler>();

        return services;
    }
}
