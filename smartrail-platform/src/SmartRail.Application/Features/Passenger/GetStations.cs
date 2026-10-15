namespace SmartRail.Application.Features.Passenger;

public sealed record StationLine(string Code, int Sequence);

public sealed record StationItem(string Code, LocalizedName Name, IReadOnlyList<StationLine> Lines);

public sealed record StationsResponse(string Version, IReadOnlyList<StationItem> Stations);

/// <summary>GET /api/v1/stations: the stations in line order (Colombo Fort first, R13) with three languages and a version (R1).</summary>
public sealed class GetStationsHandler(IPassengerQueries queries)
{
    public async Task<StationsResponse> HandleAsync(CancellationToken cancellationToken)
    {
        var records = await queries.GetStationsAsync(cancellationToken);

        // Line order: by the station's lowest sequence on any line, then by code so the order is always the same.
        var ordered = records
            .Select(r => new { Record = r, First = r.Lines.Count == 0 ? int.MaxValue : r.Lines.Min(l => l.Sequence) })
            .OrderBy(x => x.First)
            .ThenBy(x => x.Record.Code, StringComparer.Ordinal)
            .Select(x => x.Record)
            .ToList();

        var items = ordered.Select(r => new StationItem(
            r.Code,
            LocalizedName.Of(r.NameEn, r.NameSi, r.NameTa),
            r.Lines.OrderBy(l => l.Sequence).ThenBy(l => l.LineCode, StringComparer.Ordinal).Select(l => new StationLine(l.LineCode, l.Sequence)).ToList()))
            .ToList();

        return new StationsResponse(VersionOf(items), items);
    }

    /// <summary>R1: the hash covers codes, the three names and the line sequences in a fixed order, so it changes exactly when the list does.</summary>
    internal static string VersionOf(IEnumerable<StationItem> items) =>
        SmartRail.Domain.Journeys.StationListVersion.Compute(items.Select(i =>
            $"{i.Code}|{i.Name.En}|{i.Name.Si}|{i.Name.Ta}|{string.Join(',', i.Lines.Select(l => $"{l.Code}:{l.Sequence}"))}"));
}
