using Asms.Contracts.Auth;

namespace Asms.UI.Shared.Services;

public interface IAuthSession
{
    bool IsSignedIn { get; }

    Guid? UserId { get; }

    Guid? SchoolId { get; }

    IReadOnlyList<string> Roles { get; }

    IReadOnlySet<string> Permissions { get; }

    string? AccessToken { get; }

    string? RefreshToken { get; }

    DateTime? AccessTokenExpiresAtUtc { get; }

    event Action? Changed;

    void SignIn(LoginResponse response, IEnumerable<string> permissions);

    void UpdateTokens(LoginResponse response);

    void SignOut();

    bool Has(string permission);
}
