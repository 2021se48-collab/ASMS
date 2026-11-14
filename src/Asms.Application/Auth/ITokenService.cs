namespace Asms.Application.Auth;

public interface ITokenService
{
    (string Token, DateTime ExpiresAtUtc) CreateAccessToken(
        Guid userId,
        Guid schoolId,
        IReadOnlyList<string> roles,
        DateTime utcNow);
}
