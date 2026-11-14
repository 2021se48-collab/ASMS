namespace Asms.Application.Security;

/// <summary>
/// Names the claims inside a login token. The school always comes from a signed claim, never from a URL or form field.
/// </summary>
public static class AsmsClaimTypes
{
    public const string SchoolId = "asms:school_id";
    public const string UserId = "asms:user_id";
    public const string DeviceId = "asms:device_id";
    public const string Role = "asms:role";
}
