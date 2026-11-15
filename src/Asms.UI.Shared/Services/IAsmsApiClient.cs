using Asms.Contracts.Auth;

namespace Asms.UI.Shared.Services;

public interface IAsmsApiClient
{
    Task<ApiResult<LoginResponse>> LoginAsync(string userName, string password);

    Task<ApiResult<LoginResponse>> RefreshAsync();

    Task LogoutAsync();

    Task<ApiResult<PermissionsResponse>> GetPermissionsAsync();
}
