using Microsoft.EntityFrameworkCore;
using SmartRail.Domain.Journeys;
using SmartRail.Domain.Reference;

namespace SmartRail.Infrastructure.Persistence;

/// <summary>
/// The one EF Core context. The same type is used with the runtime connection (role smartrail_app) and, for
/// migrations and demo seeding, with the admin connection; only the connection string differs.
/// </summary>
public sealed class SmartrailDbContext(DbContextOptions<SmartrailDbContext> options) : DbContext(options)
{
    public DbSet<Language> Languages => Set<Language>();
    public DbSet<Line> Lines => Set<Line>();
    public DbSet<LineTranslation> LineTranslations => Set<LineTranslation>();
    public DbSet<Station> Stations => Set<Station>();
    public DbSet<StationTranslation> StationTranslations => Set<StationTranslation>();
    public DbSet<Platform> Platforms => Set<Platform>();
    public DbSet<LineStation> LineStations => Set<LineStation>();
    public DbSet<TrainService> TrainServices => Set<TrainService>();
    public DbSet<ServiceStop> ServiceStops => Set<ServiceStop>();
    public DbSet<Journey> Journeys => Set<Journey>();
    public DbSet<OperatorAssignment> OperatorAssignments => Set<OperatorAssignment>();
    public DbSet<JourneyStopState> JourneyStopStates => Set<JourneyStopState>();
    public DbSet<TrainEvent> TrainEvents => Set<TrainEvent>();
    public DbSet<JourneyException> JourneyExceptions => Set<JourneyException>();

    protected override void OnModelCreating(ModelBuilder modelBuilder) =>
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(SmartrailDbContext).Assembly);
}
