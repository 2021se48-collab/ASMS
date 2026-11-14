namespace Asms.Application.Security;

public interface IUserPermissionService
{
    Task<UserAccess> GetAccessAsync(CancellationToken ct = default);
}
