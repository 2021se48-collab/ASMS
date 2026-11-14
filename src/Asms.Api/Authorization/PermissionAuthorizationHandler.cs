using Asms.Application.Security;
using Microsoft.AspNetCore.Authorization;

namespace Asms.Api.Authorization;

public sealed class PermissionAuthorizationHandler : AuthorizationHandler<PermissionRequirement>
{
    private readonly IUserPermissionService _userPermissionService;

    public PermissionAuthorizationHandler(IUserPermissionService userPermissionService)
    {
        _userPermissionService = userPermissionService;
    }

    protected override async Task HandleRequirementAsync(
        AuthorizationHandlerContext context,
        PermissionRequirement requirement)
    {
        if (context.User.Identity?.IsAuthenticated != true)
        {
            return;
        }

        var access = await _userPermissionService.GetAccessAsync();
        if (access.Has(requirement.Permission))
        {
            context.Succeed(requirement);
        }
    }
}
