namespace Asms.Application.Auth;

public interface IAuthService
{
    Task<AuthResult> LoginAsync(
        string userName,
        string password,
        CancellationToken ct = default);
}
