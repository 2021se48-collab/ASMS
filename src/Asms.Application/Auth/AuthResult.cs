namespace Asms.Application.Auth;

public sealed record AuthResult(
    bool Succeeded,
    string? AccessToken,
    DateTime? ExpiresAtUtc,
    Guid? UserId,
    Guid? SchoolId,
    IReadOnlyList<string> Roles,
    string? RefreshToken,
    DateTime? RefreshTokenExpiresAtUtc)
{
    public static AuthResult Failed() =>
        new(false, null, null, null, null, Array.Empty<string>(), null, null);

    public static AuthResult Success(
        string accessToken,
        DateTime expiresAtUtc,
        Guid userId,
        Guid schoolId,
        IReadOnlyList<string> roles,
        string refreshToken,
        DateTime refreshTokenExpiresAtUtc) =>
        new(true, accessToken, expiresAtUtc, userId, schoolId, roles, refreshToken, refreshTokenExpiresAtUtc);
}
