namespace Asms.Application.Auth;

public interface IAuthService
{
    Task<AuthResult> LoginAsync(
        string userName,
        string password,
        CancellationToken ct = default);

    Task<AuthResult> RefreshAsync(
        string refreshToken,
        CancellationToken ct = default);

    Task LogoutAsync(
        string refreshToken,
        CancellationToken ct = default);

    Task RevokeAllForUserAsync(
        Guid identityUserId,
        string reasonCode,
        CancellationToken ct = default);
}
