namespace Asms.Contracts.Auth;

/// <summary>Contains signed access and refresh tokens and their account details.</summary>
public sealed record LoginResponse(
    string AccessToken,
    DateTime ExpiresAtUtc,
    Guid UserId,
    Guid SchoolId,
    IReadOnlyList<string> Roles,
    string RefreshToken,
    DateTime RefreshTokenExpiresAtUtc);
