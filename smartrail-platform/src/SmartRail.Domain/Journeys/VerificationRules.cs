namespace SmartRail.Domain.Journeys;

/// <summary>
/// Which delay reasons may be shown to passengers (spec 16 rule 2, FR-52, D-10, NFR-12). Only a controller can publish one,
/// and only from this approved list: "Unknown" and anything else cannot be published and must be changed first.
/// </summary>
public static class VerificationRules
{
    public const string Rejected = "Rejected";

    public static readonly IReadOnlyList<string> ApprovedReasons = ["SignalIssue", "TechnicalFault", "Obstruction", "Weather"];

    public static bool IsApproved(string? code) => code is not null && ApprovedReasons.Contains(code, StringComparer.Ordinal);
}
