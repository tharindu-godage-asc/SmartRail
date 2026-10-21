using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using SmartRail.Application.Features.Exceptions;
using SmartRail.Application.Features.Verification;
using SmartRail.Application.Features.ControllerJourneys;
using SmartRail.Application.Features.PlatformChanges;
using SmartRail.Application.Features.Journeys;
using SmartRail.Application.Features.Assignments;
using SmartRail.Application.Features.OperatorHistory;
using SmartRail.Application.Features.Passenger;
using SmartRail.Application.Features.Timetable;
using SmartRail.Infrastructure.Persistence;

namespace SmartRail.Infrastructure;

public static class DependencyInjection
{
    /// <summary>
    /// Registers infrastructure. The database is optional: it is registered only when ConnectionStrings:Smartrail is
    /// set, so environments without a database (the cluster, today) keep running and stay Ready.
    /// </summary>
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddSingleton(TimeProvider.System);
        services.Configure<DatabaseOptions>(configuration.GetSection(DatabaseOptions.Section));
        services.Configure<SeedingOptions>(configuration.GetSection(SeedingOptions.Section));

        var runtimeConnection = configuration.GetConnectionString("Smartrail");
        if (!string.IsNullOrWhiteSpace(runtimeConnection))
        {
            services.AddDbContext<SmartrailDbContext>(options => SmartrailDbContextOptions.Configure(options, runtimeConnection));
            services.AddScoped<IJourneyRepository, JourneyRepository>();
            services.AddScoped<IPassengerQueries, PassengerQueries>();
            services.AddScoped<IExceptionRepository, ExceptionRepository>();
            services.AddScoped<IVerificationRepository, VerificationRepository>();
            services.AddScoped<IControllerJourneyQueries, ControllerJourneyQueries>();
            services.AddScoped<IPlatformChangeRepository, PlatformChangeRepository>();
            services.AddScoped<IOperatorHistoryQueries, OperatorHistoryQueries>();
            services.AddScoped<ITimetableRepository, TimetableRepository>();
            services.AddScoped<IAssignmentRepository, AssignmentRepository>();
            services.Configure<OperatorDirectoryOptions>(configuration.GetSection(OperatorDirectoryOptions.Section));
            services.AddSingleton<IOperatorDirectory, ConfigOperatorDirectory>();
            services.AddHealthChecks().AddCheck<DatabaseReadinessCheck>("database", tags: ["ready"]);
            services.AddHostedService<DatabaseStartupService>();
        }

        return services;
    }
}
