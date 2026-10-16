namespace SmartRail.Infrastructure.Seeding;

/// <summary>
/// Fixed identities for the demo users. The future Keycloak realm export must create these users with the same
/// IDs, so the seeded assignments match real sign-ins. Seeding:DemoOperatorSubject can override the operator ID.
/// </summary>
public static class DemoIds
{
    /// <summary>"Nimal", the demo operator assigned to the demo journey.</summary>
    public static readonly Guid Nimal = new("d0d0d0d0-0000-4000-8000-000000000001");

    /// <summary>A second operator with no assignment, for the "not assigned" tests.</summary>
    public static readonly Guid UnassignedOperator = new("d0d0d0d0-0000-4000-8000-000000000002");

    /// <summary>"Jayasinghe", the demo controller (used by the Keycloak realm; no SmartRail table refers to him yet).</summary>
    public static readonly Guid Controller = new("d0d0d0d0-0000-4000-8000-000000000003");

    /// <summary>Spec 09 section 16: the demo train 8070 that leaves 4 minutes after the API starts. Not reference data, so these are fixed here.</summary>
    public static readonly Guid SoonService = new("00000004-0000-4000-8000-0000000000a1");

    public static Guid SoonStop(int sequence) => new($"00000005-0000-4000-8000-{0xa00 + sequence:x12}");

    public const string AssignedBy = "demo-seed";
}
