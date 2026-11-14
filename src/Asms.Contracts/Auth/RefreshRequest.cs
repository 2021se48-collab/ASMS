namespace Asms.Contracts.Auth;

/// <summary>Contains a refresh token to rotate.</summary>
public sealed record RefreshRequest(string RefreshToken);
