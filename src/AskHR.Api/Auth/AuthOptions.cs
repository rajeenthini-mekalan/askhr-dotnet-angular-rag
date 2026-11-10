namespace AskHR.Api.Auth;

/// <summary>Binds the "Auth" section of appsettings.json. No secrets here: an API only validates tokens.</summary>
public sealed class AuthOptions
{
    public const string Section = "Auth";

    public required string Authority { get; init; }
    public required string Audience { get; init; }
    public bool RequireHttpsMetadata { get; init; } = true;
}

/// <summary>Keycloak realm role names (must match askhr-realm.json).</summary>
public static class Roles
{
    public const string HrAdmin = "hr-admin";
    public const string Employee = "employee";
}

/// <summary>Authorization policy names used on endpoints.</summary>
public static class Policies
{
    public const string HrAdmin = nameof(HrAdmin);
    public const string Employee = nameof(Employee);
}
