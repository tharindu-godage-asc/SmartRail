using Microsoft.Extensions.DependencyInjection;

namespace SmartRail.Infrastructure;

public static class DependencyInjection
{
    /// <summary>Register persistence (Postgres/EF Core), identity and external clients here.</summary>
    public static IServiceCollection AddInfrastructure(this IServiceCollection services)
    {
        services.AddSingleton(TimeProvider.System);
        return services;
    }
}
