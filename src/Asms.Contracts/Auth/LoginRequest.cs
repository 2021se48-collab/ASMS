namespace Asms.Contracts.Auth;

/// <summary>Credentials used to request an access token.</summary>
public sealed record LoginRequest(string UserName, string Password);
