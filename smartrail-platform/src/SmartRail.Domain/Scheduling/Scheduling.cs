using System.Globalization;
using System.Text.RegularExpressions;
using SmartRail.Domain.Journeys;

namespace SmartRail.Domain.Scheduling;

/// <summary>A weekly service runs on its days from its first date; an extra train runs on one date (spec 13).</summary>
public enum ServiceKind { Weekly, Extra }

/// <summary>The days a service runs, written to <c>runs_on</c> in a normalised form and read back from free text (spec 13 section 5).</summary>
public static class RunDays
{
    private static readonly DayOfWeek[] Week =
        [DayOfWeek.Monday, DayOfWeek.Tuesday, DayOfWeek.Wednesday, DayOfWeek.Thursday, DayOfWeek.Friday, DayOfWeek.Saturday, DayOfWeek.Sunday];

    private static readonly string[] Codes = ["Mon", "Tue", "Wed", "Thu", "Fri", "Sat", "Sun"];

    /// <summary>Daily, Weekdays, Weekends, or a comma list such as Mon,Wed,Fri, always in week order. Null when a code is unknown or none is given.</summary>
    public static string? Normalise(IEnumerable<string> days)
    {
        var set = new HashSet<DayOfWeek>();
        foreach (var raw in days)
        {
            if (ParseDay(raw) is not { } day)
            {
                return null;
            }

            set.Add(day);
        }

        if (set.Count == 0)
        {
            return null;
        }

        if (set.Count == 7)
        {
            return "Daily";
        }

        var ordered = Week.Where(set.Contains).ToList();
        if (ordered.SequenceEqual(Week.Take(5)))
        {
            return "Weekdays";
        }

        if (ordered.SequenceEqual(Week.Skip(5)))
        {
            return "Weekends";
        }

        return string.Join(',', ordered.Select(d => Codes[Array.IndexOf(Week, d)]));
    }

    /// <summary>The weekdays of a <c>runs_on</c> text, or null when it is not a form this system understands (never guessed, spec 04 R6).</summary>
    public static IReadOnlySet<DayOfWeek>? Parse(string? runsOn)
    {
        var text = runsOn?.Trim();
        switch (text?.ToLowerInvariant())
        {
            case null or "":
                return null;
            case "daily":
                return Week.ToHashSet();
            case "weekdays":
                return Week.Take(5).ToHashSet();
            case "weekends":
                return Week.Skip(5).ToHashSet();
        }

        var set = new HashSet<DayOfWeek>();
        foreach (var part in text!.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries))
        {
            if (ParseDay(part) is not { } day)
            {
                return null;
            }

            set.Add(day);
        }

        return set.Count == 0 ? null : set;
    }

    private static DayOfWeek? ParseDay(string code) => code.Trim().ToLowerInvariant() switch
    {
        "mon" or "monday" => DayOfWeek.Monday,
        "tue" or "tuesday" => DayOfWeek.Tuesday,
        "wed" or "wednesday" => DayOfWeek.Wednesday,
        "thu" or "thursday" => DayOfWeek.Thursday,
        "fri" or "friday" => DayOfWeek.Friday,
        "sat" or "saturday" => DayOfWeek.Saturday,
        "sun" or "sunday" => DayOfWeek.Sunday,
        _ => null,
    };
}

/// <summary>The one place that decides whether a service runs on a date: its effective range and its days (spec 13 rule 9; cancellation arrives with S3).</summary>
public static class ServiceRuns
{
    /// <summary>True or false; null when the days text is not understood (an unsupported calendar is never guessed).</summary>
    public static bool? On(string? runsOn, DateOnly effectiveFrom, DateOnly? effectiveTo, DateOnly date)
    {
        if (date < effectiveFrom || (effectiveTo is { } to && date > to))
        {
            return false;
        }

        // The three fixed words keep their older reading in one place; a day list is read here.
        return RunDays.Parse(runsOn) is { } days ? days.Contains(date.DayOfWeek) : null;
    }

    /// <summary>The same decision, also false on a cancelled date of the train number (spec 14 rule 7).</summary>
    public static bool? On(string? runsOn, DateOnly effectiveFrom, DateOnly? effectiveTo, DateOnly date, IEnumerable<CancellationRange>? cancellations) =>
        cancellations is not null && cancellations.Any(c => c.Covers(date)) ? false : On(runsOn, effectiveFrom, effectiveTo, date);
}

/// <summary>A cancelled range of dates of a train number: one date, or from a date onward when <paramref name="To"/> is null (spec 14).</summary>
public sealed record CancellationRange(DateOnly From, DateOnly? To)
{
    public bool Covers(DateOnly date) => date >= From && (To is null || date <= To);
}

public sealed record StopDraft(string StationCode, string? Arrival, string? Departure, string? Platform);

/// <summary>What a controller sends to create a service (spec 13 section 4.5).</summary>
public sealed record ServiceDraft(
    ServiceKind Kind,
    string TrainNumber,
    string LineCode,
    string Category,
    string ServiceType,
    IReadOnlyList<string> Days,
    IReadOnlyList<DateOnly> Dates,
    DateOnly? EffectiveFrom,
    IReadOnlyList<StopDraft> Stops);

public sealed record ValidationIssue(string Code, string Field, string Message);

public sealed record ValidationResult(IReadOnlyList<ValidationIssue> Errors, IReadOnlyList<ValidationIssue> Warnings)
{
    public bool Valid => Errors.Count == 0;
}

/// <summary>A station of the line as the rules need it: its place in the line and its platforms.</summary>
public sealed record LineStationInfo(string Code, string Name, int Sequence, IReadOnlyList<string> Platforms);

/// <summary>Another service the new one is compared with: its days, dates and stops, for the platform advice.</summary>
public sealed record ExistingService(
    Guid ServiceId,
    string TrainNumber,
    string RunsOn,
    DateOnly EffectiveFrom,
    DateOnly? EffectiveTo,
    IReadOnlyList<ExistingStop> Stops);

public sealed record ExistingStop(string StationCode, string? Platform, TimeOnly? Arrival, TimeOnly? Departure);

/// <summary>
/// The rules of spec 13 section 6 as pure functions: dates not in the past, stops in line order, times that increase, platforms of the
/// right station (errors), and a platform used by another train within a few minutes (advice only, FR-57).
/// </summary>
public static partial class ServiceValidator
{
    [GeneratedRegex("^[A-Za-z0-9]{1,10}$")]
    private static partial Regex TrainNumberPattern();

    public static ValidationResult Validate(
        ServiceDraft draft,
        IReadOnlyList<LineStationInfo> line,
        IReadOnlyList<ExistingService> others,
        DateOnly today,
        TimeOnly nowLocal,
        int platformWarningMinutes)
    {
        var errors = new List<ValidationIssue>();
        var warnings = new List<ValidationIssue>();
        void Error(string code, string field, string message) => errors.Add(new ValidationIssue(code, field, message));

        if (!TrainNumberPattern().IsMatch(draft.TrainNumber ?? ""))
        {
            Error("train_number_invalid", "trainNumber", "The train number is 1 to 10 letters or digits.");
        }

        if (!Enum.TryParse<Domain.Reference.ServiceCategory>(draft.Category, true, out _))
        {
            Error("category_invalid", "category", "The category is Regional or LongDistance.");
        }

        if (!Enum.TryParse<Domain.Reference.ServiceType>(draft.ServiceType, true, out _))
        {
            Error("service_type_invalid", "serviceType", "The service type is Slow or Express.");
        }

        var firstDates = new List<DateOnly>();
        if (draft.Kind == ServiceKind.Weekly)
        {
            if (RunDays.Normalise(draft.Days ?? []) is null)
            {
                Error("days_required", "days", "Choose at least one day, using Mon to Sun.");
            }

            if (draft.EffectiveFrom is not { } from)
            {
                Error("effective_from_required", "effectiveFrom", "Say the first date the service runs.");
            }
            else
            {
                firstDates.Add(from);
            }
        }
        else
        {
            if (draft.Dates is null || draft.Dates.Count == 0)
            {
                Error("dates_required", "dates", "An extra train needs at least one date.");
            }
            else
            {
                firstDates.AddRange(draft.Dates);
            }
        }

        foreach (var date in firstDates.Where(d => d < today))
        {
            Error("date_in_past", draft.Kind == ServiceKind.Weekly ? "effectiveFrom" : "dates", $"{date:yyyy-MM-dd} is in the past.");
        }

        ValidateStops(draft, line, today, nowLocal, firstDates, errors);

        if (errors.Count == 0)
        {
            AddPlatformWarnings(draft, others, firstDates, platformWarningMinutes, warnings);
        }

        return new ValidationResult(errors, warnings);
    }

    private static void ValidateStops(
        ServiceDraft draft, IReadOnlyList<LineStationInfo> line, DateOnly today, TimeOnly nowLocal, List<DateOnly> firstDates, List<ValidationIssue> errors)
    {
        void Error(string code, string field, string message) => errors.Add(new ValidationIssue(code, field, message));

        var stops = draft.Stops ?? [];
        if (stops.Count < 2)
        {
            Error("stops_too_few", "stops", "A service needs at least two stops.");
            return;
        }

        var infos = new List<LineStationInfo?>();
        for (var i = 0; i < stops.Count; i++)
        {
            var info = line.FirstOrDefault(l => l.Code.Equals(stops[i].StationCode, StringComparison.OrdinalIgnoreCase));
            infos.Add(info);
            if (info is null)
            {
                Error("station_not_on_line", $"stops[{i}].stationCode", $"{stops[i].StationCode} is not a station of line {draft.LineCode}.");
            }
        }

        if (infos.Any(i => i is null))
        {
            return;
        }

        var repeated = infos.GroupBy(i => i!.Code).FirstOrDefault(g => g.Count() > 1);
        if (repeated is not null)
        {
            Error("station_repeated", "stops", $"{repeated.First()!.Name} appears twice.");
            return;
        }

        var sequences = infos.Select(i => i!.Sequence).ToList();
        var ascending = sequences[0] < sequences[^1];
        for (var i = 1; i < sequences.Count; i++)
        {
            if (ascending ? sequences[i] < sequences[i - 1] : sequences[i] > sequences[i - 1])
            {
                Error("stops_not_in_line_order", $"stops[{i}].stationCode", $"{infos[i]!.Name} is out of line order after {infos[i - 1]!.Name}.");
                return;
            }
        }

        var times = new List<(TimeOnly? Arrival, TimeOnly? Departure)>();
        for (var i = 0; i < stops.Count; i++)
        {
            var first = i == 0;
            var last = i == stops.Count - 1;
            var arrival = ParseTime(stops[i].Arrival, $"stops[{i}].arrival", errors);
            var departure = ParseTime(stops[i].Departure, $"stops[{i}].departure", errors);
            if (first && stops[i].Arrival is not null)
            {
                Error("first_stop_departure_only", $"stops[{i}].arrival", $"{infos[i]!.Name} is the first stop: give a departure only.");
            }

            if (last && stops[i].Departure is not null)
            {
                Error("last_stop_arrival_only", $"stops[{i}].departure", $"{infos[i]!.Name} is the last stop: give an arrival only.");
            }

            if (first && stops[i].Departure is null)
            {
                Error("stop_time_required", $"stops[{i}].departure", $"{infos[i]!.Name} needs a departure time.");
            }

            if (last && stops[i].Arrival is null)
            {
                Error("stop_time_required", $"stops[{i}].arrival", $"{infos[i]!.Name} needs an arrival time.");
            }

            if (!first && !last && (stops[i].Arrival is null || stops[i].Departure is null))
            {
                Error("stop_time_required", $"stops[{i}].arrival", $"{infos[i]!.Name} needs an arrival and a departure time.");
            }

            times.Add((arrival, departure));

            var platform = stops[i].Platform;
            if (platform is not null && !infos[i]!.Platforms.Contains(platform, StringComparer.OrdinalIgnoreCase))
            {
                Error("platform_not_at_station", $"stops[{i}].platform", $"Platform {platform} is not a platform of {infos[i]!.Name}.");
            }
        }

        if (errors.Count > 0)
        {
            return;
        }

        // Times must increase through the whole service, within one calendar day in this slice.
        TimeOnly? previous = null;
        for (var i = 0; i < stops.Count; i++)
        {
            foreach (var (time, label) in new[] { (times[i].Arrival, "arrival"), (times[i].Departure, "departure") })
            {
                if (time is not { } t)
                {
                    continue;
                }

                if (previous is { } p && t <= p)
                {
                    Error("times_not_increasing", $"stops[{i}].{label}", $"{infos[i]!.Name} {t:HH\\:mm} is not after {p:HH\\:mm}.");
                    return;
                }

                previous = t;
            }
        }

        if (firstDates.Any(d => d == today) && times[0].Departure is { } firstDeparture && firstDeparture <= nowLocal)
        {
            Error("starts_in_past", "stops[0].departure", $"The first departure {firstDeparture:HH\\:mm} is not later than now.");
        }
    }

    private static TimeOnly? ParseTime(string? text, string field, List<ValidationIssue> errors)
    {
        if (text is null)
        {
            return null;
        }

        if (TimeOnly.TryParseExact(text, "HH:mm", CultureInfo.InvariantCulture, DateTimeStyles.None, out var time))
        {
            return time;
        }

        errors.Add(new ValidationIssue("time_invalid", field, $"{text} is not a time written HH:mm."));
        return null;
    }

    private static void AddPlatformWarnings(
        ServiceDraft draft, IReadOnlyList<ExistingService> others, List<DateOnly> firstDates, int minutes, List<ValidationIssue> warnings)
    {
        var mine = draft.Kind == ServiceKind.Weekly
            ? RunDays.Parse(RunDays.Normalise(draft.Days))
            : firstDates.Select(d => d.DayOfWeek).ToHashSet();
        var myFrom = firstDates.Min();
        DateOnly? myTo = draft.Kind == ServiceKind.Weekly ? null : firstDates.Max();
        if (mine is null)
        {
            return;
        }

        for (var i = 0; i < draft.Stops.Count; i++)
        {
            var stop = draft.Stops[i];
            if (stop.Platform is null)
            {
                continue;
            }

            var at = stop.Arrival ?? stop.Departure;
            if (at is null || !TimeOnly.TryParseExact(at, "HH:mm", CultureInfo.InvariantCulture, DateTimeStyles.None, out var myTime))
            {
                continue;
            }

            foreach (var other in others)
            {
                // The same train number is another matter (a clash of numbers, answered 409), not a platform clash.
                if (other.TrainNumber.Equals(draft.TrainNumber, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                var theirDays = RunDays.Parse(other.RunsOn);
                var overlaps = other.EffectiveFrom <= (myTo ?? DateOnly.MaxValue) && (other.EffectiveTo ?? DateOnly.MaxValue) >= myFrom;
                if (theirDays is null || !overlaps || !theirDays.Overlaps(mine))
                {
                    continue;
                }

                foreach (var their in other.Stops.Where(s => s.StationCode.Equals(stop.StationCode, StringComparison.OrdinalIgnoreCase)
                                                             && string.Equals(s.Platform, stop.Platform, StringComparison.OrdinalIgnoreCase)))
                {
                    var theirTime = their.Arrival ?? their.Departure;
                    if (theirTime is { } tt && Math.Abs((tt.ToTimeSpan() - myTime.ToTimeSpan()).TotalMinutes) < minutes)
                    {
                        warnings.Add(new ValidationIssue(
                            "platform_close", $"stops[{i}].platform",
                            $"Train {other.TrainNumber} uses {stop.StationCode} platform {stop.Platform} at {tt:HH\\:mm} ({Math.Abs((int)(tt.ToTimeSpan() - myTime.ToTimeSpan()).TotalMinutes)} minutes apart)."));
                    }
                }
            }
        }
    }
}

/// <summary>What changed between two versions of a service, as short readable lines for the Versions list (spec 14 section 7).</summary>
public static class AmendmentDiff
{
    public static IReadOnlyList<string> Describe(
        IReadOnlyList<ExistingStop> before, IReadOnlyList<ExistingStop> after, IReadOnlyDictionary<string, string> names, string? runsBefore, string? runsAfter)
    {
        var changes = new List<string>();
        string Name(string code) => names.GetValueOrDefault(code, code);
        string Time(TimeOnly? t) => t is { } v ? v.ToString("HH\\:mm", CultureInfo.InvariantCulture) : "-";

        foreach (var stop in before.Where(b => after.All(a => a.StationCode != b.StationCode)))
        {
            changes.Add($"Removed stop {Name(stop.StationCode)}");
        }

        foreach (var stop in after)
        {
            var was = before.FirstOrDefault(b => b.StationCode == stop.StationCode);
            if (was is null)
            {
                changes.Add($"Added stop {Name(stop.StationCode)} {Time(stop.Arrival ?? stop.Departure)}");
                continue;
            }

            if (was.Arrival != stop.Arrival)
            {
                changes.Add($"{Name(stop.StationCode)} arrives {Time(stop.Arrival)} (was {Time(was.Arrival)})");
            }

            if (was.Departure != stop.Departure)
            {
                changes.Add($"{Name(stop.StationCode)} {Time(stop.Departure)} (was {Time(was.Departure)})");
            }

            if (!string.Equals(was.Platform, stop.Platform, StringComparison.OrdinalIgnoreCase))
            {
                changes.Add($"{Name(stop.StationCode)} platform {stop.Platform ?? "not set"} (was {was.Platform ?? "not set"})");
            }
        }

        if (!string.Equals(runsBefore, runsAfter, StringComparison.OrdinalIgnoreCase))
        {
            changes.Add($"Days {runsAfter} (was {runsBefore})");
        }

        return changes;
    }
}
