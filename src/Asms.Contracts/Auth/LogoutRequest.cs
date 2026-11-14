namespace Asms.Contracts.Auth;

/// <summary>Contains a refresh token whose session family should be revoked.</summary>
public sealed record LogoutRequest(string RefreshToken);
