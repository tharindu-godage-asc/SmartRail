namespace SmartRail.Api.Authentication;

/// <summary>Role names as they appear in the token's flat "roles" claim, and the policies built on them. One place only.</summary>
public static class Roles
{
    public const string Passenger = "passenger";
    public const string Operator = "operator";
    public const string Controller = "controller";
    public const string Caterer = "caterer";
    public const string SystemAdmin = "system-admin";

    public const string ClaimType = "roles";
}

public static class Policies
{
    public const string Operator = "Operator";
    public const string Controller = "Controller";
    public const string Caterer = "Caterer";
}
