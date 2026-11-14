using Asms.Domain.Security;
using Microsoft.AspNetCore.Authorization;

namespace Asms.Api.Authorization;

public sealed class HasPermissionAttribute : AuthorizeAttribute
{
    public HasPermissionAttribute(string permission)
    {
        if (string.IsNullOrWhiteSpace(permission) ||
            !Permissions.All.Contains(permission, StringComparer.Ordinal))
        {
            throw new ArgumentException("The permission is not defined.", nameof(permission));
        }

        Policy = $"perm:{permission}";
    }
}
