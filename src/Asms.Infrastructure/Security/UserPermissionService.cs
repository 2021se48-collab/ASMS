using System.Collections.Frozen;
using System.Data.Common;
using Asms.Application.Abstractions;
using Asms.Application.Security;
using Asms.Domain.Security;
using Asms.Infrastructure.Identity;
using Asms.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Asms.Infrastructure.Security;

public sealed class UserPermissionService : IUserPermissionService
{
    private static readonly FrozenSet<string> KnownPermissions =
        Permissions.All.ToFrozenSet(StringComparer.Ordinal);

    private readonly AsmsDbContext _db;
    private readonly AsmsIdentityDbContext _identityDb;
    private readonly ICurrentTenant _tenant;
    private readonly object _cacheLock = new();
    private Task<UserAccess>? _cachedAccess;

    public UserPermissionService(
        AsmsDbContext db,
        AsmsIdentityDbContext identityDb,
        ICurrentTenant tenant)
    {
        _db = db;
        _identityDb = identityDb;
        _tenant = tenant;
    }

    public Task<UserAccess> GetAccessAsync(CancellationToken ct = default)
    {
        lock (_cacheLock)
        {
            return _cachedAccess ??= LoadAccessAsync(ct);
        }
    }

    private async Task<UserAccess> LoadAccessAsync(CancellationToken ct)
    {
        if (_tenant.SchoolId is not Guid schoolId ||
            _tenant.UserId is not Guid userId)
        {
            return UserAccess.Denied;
        }

        try
        {
            var identityUser = await _identityDb.Users
                .AsNoTracking()
                .SingleOrDefaultAsync(user => user.Id == userId, ct);
            if (identityUser is null ||
                !identityUser.IsActive ||
                identityUser.SchoolId != schoolId ||
                identityUser.SchoolId == Guid.Empty ||
                identityUser.AppUserId is not Guid appUserId)
            {
                return UserAccess.Denied;
            }

            var school = await _db.Schools
                .IgnoreQueryFilters()
                .AsNoTracking()
                .SingleOrDefaultAsync(item => item.Id == schoolId, ct);
            if (school is null || school.IsDeleted || !school.IsActive)
            {
                return UserAccess.Denied;
            }

            var appUserExists = await _db.AppUsers
                .IgnoreQueryFilters()
                .AsNoTracking()
                .AnyAsync(
                    user => user.Id == appUserId &&
                        user.SchoolId == schoolId &&
                        !user.IsDeleted &&
                        user.IsActive,
                    ct);
            if (!appUserExists)
            {
                return UserAccess.Denied;
            }

            var roles = await (
                from userRole in _db.UserRoles.IgnoreQueryFilters().AsNoTracking()
                join role in _db.AppRoles.IgnoreQueryFilters().AsNoTracking()
                    on userRole.RoleId equals role.Id
                where userRole.SchoolId == schoolId &&
                    !userRole.IsDeleted &&
                    userRole.UserId == appUserId &&
                    role.SchoolId == schoolId &&
                    !role.IsDeleted
                select new { role.Id, role.Name })
                .Distinct()
                .ToListAsync(ct);
            if (roles.Count == 0)
            {
                return UserAccess.Denied;
            }

            var roleIds = roles.Select(role => role.Id).ToArray();
            var permissionNames = await _db.RolePermissions
                .IgnoreQueryFilters()
                .AsNoTracking()
                .Where(permission =>
                    permission.SchoolId == schoolId &&
                    !permission.IsDeleted &&
                    roleIds.Contains(permission.RoleId))
                .Select(permission => permission.Permission)
                .Distinct()
                .ToListAsync(ct);

            var permissions = permissionNames
                .Where(KnownPermissions.Contains)
                .ToFrozenSet(StringComparer.Ordinal);
            var roleNames = roles
                .Select(role => role.Name)
                .Distinct(StringComparer.Ordinal)
                .OrderBy(role => role, StringComparer.Ordinal)
                .ToArray();

            return new UserAccess(true, Array.AsReadOnly(roleNames), permissions);
        }
        catch (DbException)
        {
            return UserAccess.Denied;
        }
        catch (InvalidOperationException)
        {
            return UserAccess.Denied;
        }
    }
}
