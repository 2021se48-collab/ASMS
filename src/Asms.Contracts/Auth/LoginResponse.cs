namespace Asms.Contracts.Auth;

/// <summary>Contains a signed access token and its account details.</summary>
public sealed record LoginResponse(
    string AccessToken,
    DateTime ExpiresAtUtc,
    Guid UserId,
    Guid SchoolId,
    IReadOnlyList<string> Roles);
