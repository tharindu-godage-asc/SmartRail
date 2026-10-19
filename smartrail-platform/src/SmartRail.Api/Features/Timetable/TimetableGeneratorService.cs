using SmartRail.Application.Features.Timetable;

namespace SmartRail.Api.Features.Timetable;

/// <summary>
/// Creates the journeys of the next days that are missing (FR-11, spec 13 rule 9): at start-up and then every
/// <c>Scheduling:GenerateEveryMinutes</c>. Idempotent; a failed run is logged and tried again next time. Runs only when
/// <c>Scheduling:GenerateJourneys</c> is true.
/// </summary>
public sealed class TimetableGeneratorService(IServiceScopeFactory scopes, SchedulingSettings settings, TimeProvider clock, ILogger<TimetableGeneratorService> logger)
    : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!settings.GenerateJourneys)
        {
            return;
        }

        var interval = TimeSpan.FromMinutes(Math.Max(1, settings.GenerateEveryMinutes));
        using var timer = new PeriodicTimer(interval, clock);
        do
        {
            try
            {
                await using var scope = scopes.CreateAsyncScope();
                var created = await scope.ServiceProvider.GetRequiredService<JourneyGenerationHandler>().RunAsync(stoppingToken);
                logger.LogInformation("Journey generator ran: {Created} journeys created for the next {Days} days.", created, settings.HorizonDays);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogError(ex, "Journey generator failed; it will run again in {Minutes} minutes.", interval.TotalMinutes);
            }
        }
        while (await timer.WaitForNextTickAsync(stoppingToken));
    }
}
