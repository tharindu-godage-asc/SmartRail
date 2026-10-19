using System.Security.Cryptography;
using System.Text;

namespace SmartRail.Domain.Journeys;

/// <summary>R5 and R6: which days a service runs, from the free-text <c>runs_on</c> column.</summary>
public static class ServiceCalendar
{
    /// <summary>
    /// True or false for Daily, Weekdays and Weekends (case-insensitive, trimmed), using the weekday of the operating date.
    /// A list of days such as Mon,Wed,Fri is read too (spec 13). Null when the text is anything else: an unsupported calendar is never guessed (R6). Holidays are not modelled (D1).
    /// </summary>
    public static bool? Runs(string? runsOn, DateOnly date) =>
        Scheduling.RunDays.Parse(runsOn) is { } days ? days.Contains(date.DayOfWeek) : null;
}

/// <summary>
/// A stop of a journey and whether anything was recorded for it (Pending means nothing). <paramref name="Left"/> is true when
/// the train's departure from the stop was reported; a stop with only an arrival is Reported but not left.
/// </summary>
public sealed record StopMark(int Sequence, StopConfirmation Confirmation, bool Left = true);

public static class StopPassed
{
    /// <summary>
    /// R4.1: the train has already left a stop when a LATER stop has any report or was inferred (it cannot be further on
    /// without having left), or when this stop itself was inferred (skipped) or its departure was reported. A stop with
    /// only an arrival report has not been left yet.
    /// </summary>
    public static bool HasDeparted(IEnumerable<StopMark> stops, int stopSequence) =>
        stops.Any(s => s.Confirmation != StopConfirmation.Pending
            && (s.Sequence > stopSequence
                || (s.Sequence == stopSequence && (s.Confirmation == StopConfirmation.Inferred || s.Left))));
}

/// <summary>R12: how an <c>If-None-Match</c> header is compared with the current entity tag.</summary>
public static class EntityTagMatcher
{
    /// <summary>
    /// The header may hold several tags, weak tags (<c>W/"abc"</c>) and <c>*</c>. True (so the answer is 304) when
    /// <c>*</c> is present or any listed tag equals <paramref name="currentTag"/> after removing <c>W/</c> and quotes
    /// (weak comparison). A missing or malformed header never matches.
    /// </summary>
    public static bool Matches(string? ifNoneMatch, string currentTag)
    {
        if (string.IsNullOrWhiteSpace(ifNoneMatch))
        {
            return false;
        }

        var current = Normalise(currentTag);
        foreach (var raw in ifNoneMatch.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries))
        {
            if (raw == "*")
            {
                return true;
            }

            if (Normalise(raw) == current)
            {
                return true;
            }
        }

        return false;
    }

    private static string Normalise(string tag)
    {
        var value = tag.Trim();
        if (value.StartsWith("W/", StringComparison.OrdinalIgnoreCase))
        {
            value = value[2..];
        }

        return value.Trim('"');
    }
}

/// <summary>R1: a content hash of the station list, so the phone knows when its offline copy is out of date.</summary>
public static class StationListVersion
{
    /// <summary>SHA-256 of the canonical text, shortened to 16 lowercase hex characters. Equal content always gives an equal version.</summary>
    public static string Compute(IEnumerable<string> canonicalLines)
    {
        var text = string.Join('\n', canonicalLines);
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(text));
        return Convert.ToHexString(hash)[..16].ToLowerInvariant();
    }
}
