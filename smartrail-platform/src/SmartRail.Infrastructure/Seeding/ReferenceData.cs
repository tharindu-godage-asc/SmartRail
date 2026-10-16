using SmartRail.Domain.Reference;

namespace SmartRail.Infrastructure.Seeding;

/// <summary>
/// The small, stable reference data that every environment gets from migration 0001 (EF Core HasData).
/// Every row has a fixed GUID so all environments are identical. Changing a value later needs a new migration.
/// Placeholders (to verify before the demo): distances, the operations phone, the express service 8061 and its
/// times, and the Tamil names, which still need native-speaker validation (see the discovery document).
/// </summary>
public static class ReferenceData
{
    // Fixed, readable GUIDs: the first block says which table the row is for.
    private static Guid Id(int group, int n) => new($"{group:x8}-0000-4000-8000-{n:x12}");

    public static readonly Guid LineMain = Id(1, 1);

    public static readonly Guid StationColomboFort = Id(2, 1);
    public static readonly Guid StationMaradana = Id(2, 2);
    public static readonly Guid StationRagama = Id(2, 3);
    public static readonly Guid StationGampaha = Id(2, 4);
    public static readonly Guid StationVeyangoda = Id(2, 5);

    public static readonly Guid Service8058 = Id(4, 1);
    public static readonly Guid Service8061 = Id(4, 2);
    public static readonly Guid Service8075 = Id(4, 3);

    public static IReadOnlyList<Language> Languages { get; } =
    [
        new() { Code = "en", Name = "English", IsDefault = true },
        new() { Code = "si", Name = "Sinhala" },
        new() { Code = "ta", Name = "Tamil" },
    ];

    public static IReadOnlyList<Line> Lines { get; } =
    [
        // The phone number is a placeholder for the operations desk (FR-29).
        new() { Id = LineMain, Code = "MAIN", Name = "Main Line", OperationsPhone = "+94 11 000 0000" },
    ];

    public static IReadOnlyList<LineTranslation> LineTranslations { get; } =
    [
        new() { LineId = LineMain, LanguageCode = "si", Name = "ප්‍රධාන මාර්ගය" },
        new() { LineId = LineMain, LanguageCode = "ta", Name = "பிரதான பாதை" },
    ];

    public static IReadOnlyList<Station> Stations { get; } =
    [
        new() { Id = StationColomboFort, Code = "CF", Name = "Colombo Fort" },
        new() { Id = StationMaradana, Code = "MDA", Name = "Maradana" },
        new() { Id = StationRagama, Code = "RGM", Name = "Ragama" },
        new() { Id = StationGampaha, Code = "GPH", Name = "Gampaha" },
        new() { Id = StationVeyangoda, Code = "VYG", Name = "Veyangoda" },
    ];

    public static IReadOnlyList<StationTranslation> StationTranslations { get; } =
    [
        new() { StationId = StationColomboFort, LanguageCode = "si", Name = "කොළඹ කොටුව" },
        new() { StationId = StationColomboFort, LanguageCode = "ta", Name = "கொழும்பு கோட்டை" },
        new() { StationId = StationMaradana, LanguageCode = "si", Name = "මරදාන" },
        new() { StationId = StationMaradana, LanguageCode = "ta", Name = "மருதானை" },
        new() { StationId = StationRagama, LanguageCode = "si", Name = "රාගම" },
        new() { StationId = StationRagama, LanguageCode = "ta", Name = "ராகம" },
        new() { StationId = StationGampaha, LanguageCode = "si", Name = "ගම්පහ" },
        new() { StationId = StationGampaha, LanguageCode = "ta", Name = "கம்பஹா" },
        new() { StationId = StationVeyangoda, LanguageCode = "si", Name = "වේයන්ගොඩ" },
        new() { StationId = StationVeyangoda, LanguageCode = "ta", Name = "வேயங்கொடை" },
    ];

    /// <summary>Platforms 1 and 2 at every station, so a platform change (FR-57) can be demonstrated.</summary>
    public static IReadOnlyList<Platform> Platforms { get; } = BuildPlatforms();

    /// <summary>Colombo Fort is the line origin (sequence 1). The distances are rounded placeholders.</summary>
    public static IReadOnlyList<LineStation> LineStations { get; } =
    [
        new() { LineId = LineMain, StationId = StationColomboFort, Sequence = 1, DistanceKm = 0.0m },
        new() { LineId = LineMain, StationId = StationMaradana, Sequence = 2, DistanceKm = 2.0m },
        new() { LineId = LineMain, StationId = StationRagama, Sequence = 3, DistanceKm = 13.0m },
        new() { LineId = LineMain, StationId = StationGampaha, Sequence = 4, DistanceKm = 28.0m },
        new() { LineId = LineMain, StationId = StationVeyangoda, Sequence = 5, DistanceKm = 38.0m },
    ];

    public static IReadOnlyList<TrainService> TrainServices { get; } =
    [
        // 8058 runs from Veyangoda toward Colombo Fort, the line origin, so it is Inbound.
        new()
        {
            Id = Service8058, TrainNumber = "8058", LineId = LineMain, ServiceType = ServiceType.Slow,
            ServiceCategory = ServiceCategory.Regional, Direction = ServiceDirection.Inbound, RunsOn = "Weekdays",
        },
        // The express example (FR-15): a different stopping pattern that skips Ragama and Maradana. Placeholder number.
        new()
        {
            Id = Service8061, TrainNumber = "8061", LineId = LineMain, ServiceType = ServiceType.Express,
            ServiceCategory = ServiceCategory.Regional, Direction = ServiceDirection.Inbound, RunsOn = "Weekdays",
        },
        // Spec 09 section 16: a night train, so every board has an upcoming train late in the evening. Runs every day.
        new()
        {
            Id = Service8075, TrainNumber = "8075", LineId = LineMain, ServiceType = ServiceType.Slow,
            ServiceCategory = ServiceCategory.Regional, Direction = ServiceDirection.Inbound, RunsOn = "Daily",
        },
    ];

    public static IReadOnlyList<ServiceStop> ServiceStops { get; } =
    [
        Stop(11, Service8058, StationVeyangoda, 1, null, new(8, 20)),
        Stop(12, Service8058, StationGampaha, 2, new(8, 30), new(8, 31), arrivalReportRequired: true), // designated arrival stop (D-08)
        Stop(13, Service8058, StationRagama, 3, new(8, 42), new(8, 43)),
        Stop(14, Service8058, StationMaradana, 4, new(8, 50), new(8, 51)),
        Stop(15, Service8058, StationColomboFort, 5, new(8, 55), null),

        Stop(21, Service8061, StationVeyangoda, 1, null, new(7, 50)),
        Stop(22, Service8061, StationGampaha, 2, new(8, 0), new(8, 1)),
        Stop(23, Service8061, StationColomboFort, 3, new(8, 30), null),

        Stop(31, Service8075, StationVeyangoda, 1, null, new(22, 10)),
        Stop(32, Service8075, StationGampaha, 2, new(22, 20), new(22, 21)),
        Stop(33, Service8075, StationRagama, 3, new(22, 32), new(22, 33)),
        Stop(34, Service8075, StationMaradana, 4, new(22, 40), new(22, 41)),
        Stop(35, Service8075, StationColomboFort, 5, new(22, 45), null),
    ];

    public static Guid StopId(Guid serviceId, int sequence) =>
        ServiceStops.Single(s => s.ServiceId == serviceId && s.Sequence == sequence).Id;

    private static ServiceStop Stop(int n, Guid serviceId, Guid stationId, int sequence, TimeOnly? arrival, TimeOnly? departure,
        bool arrivalReportRequired = false) => new()
    {
        Id = Id(5, n), ServiceId = serviceId, StationId = stationId, Sequence = sequence,
        ScheduledArrival = arrival, ScheduledDeparture = departure, ArrivalReportRequired = arrivalReportRequired,
        PlannedPlatformId = PlatformOne(stationId),
    };

    /// <summary>
    /// Spec 09: the demo services all use platform 1 at every station, so a platform display has something to show and platform 2
    /// stays empty (the honest "no train" state). The platform IDs are the ones BuildPlatforms creates.
    /// </summary>
    internal static Guid PlatformOne(Guid stationId)
    {
        var stations = new[] { StationColomboFort, StationMaradana, StationRagama, StationGampaha, StationVeyangoda };
        return Id(3, ((Array.IndexOf(stations, stationId) + 1) * 10) + 1);
    }

    private static List<Platform> BuildPlatforms()
    {
        var stations = new[] { StationColomboFort, StationMaradana, StationRagama, StationGampaha, StationVeyangoda };
        var platforms = new List<Platform>();
        for (var s = 0; s < stations.Length; s++)
        {
            for (var p = 1; p <= 2; p++)
            {
                platforms.Add(new Platform { Id = Id(3, ((s + 1) * 10) + p), StationId = stations[s], Code = p.ToString() });
            }
        }

        return platforms;
    }
}
