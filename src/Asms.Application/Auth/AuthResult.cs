namespace Asms.Application.Auth;

public sealed record AuthResult(
    bool Succeeded,
    string? AccessToken,
    DateTime? ExpiresAtUtc,
    Guid? UserId,
    Guid? SchoolId,
    IReadOnlyList<string> Roles)
{
    public static AuthResult Failed() =>
        new(false, null, null, null, null, Array.Empty<string>());

    public static AuthResult Success(
        string accessToken,
        DateTime expiresAtUtc,
        Guid userId,
        Guid schoolId,
        IReadOnlyList<string> roles) =>
        new(true, accessToken, expiresAtUtc, userId, schoolId, roles);
}
