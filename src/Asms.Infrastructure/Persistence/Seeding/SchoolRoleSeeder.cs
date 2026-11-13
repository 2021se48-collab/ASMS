using Asms.Application.Abstractions;
using Asms.Domain.Security;
using Asms.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Asms.Infrastructure.Persistence.Seeding;

public sealed record SeedResult(
    int RolesAdded,
    int RolesRestored,
    int PermissionsAdded,
    int PermissionsRestored);

public class SchoolRoleSeeder
{
    private readonly AsmsDbContext _db;
    private readonly ICurrentTenant _tenant;

    public SchoolRoleSeeder(AsmsDbContext db, ICurrentTenant tenant)
    {
        _db = db;
        _tenant = tenant;
    }

    public async Task<SeedResult> SeedAsync(Guid schoolId, CancellationToken ct = default)
    {
        if (schoolId == Guid.Empty)
        {
            throw new ArgumentException("School ID cannot be empty.", nameof(schoolId));
        }

        if (_tenant.SchoolId != schoolId)
        {
            throw new InvalidOperationException("Tenant does not match the school being seeded.");
        }

        var roles = await _db.AppRoles
            .IgnoreQueryFilters()
            .Where(role => role.SchoolId == schoolId)
            .ToListAsync(ct);
        var permissions = await _db.RolePermissions
            .IgnoreQueryFilters()
            .Where(permission => permission.SchoolId == schoolId)
            .ToListAsync(ct);

        var rolesAdded = 0;
        var rolesRestored = 0;
        var permissionsAdded = 0;
        var permissionsRestored = 0;

        foreach (var roleName in DefaultRolePermissions.SchoolRoles)
        {
            var role = roles.SingleOrDefault(existing => existing.Name == roleName);
            if (role is null)
            {
                role = new AppRole
                {
                    SchoolId = schoolId,
                    Name = roleName,
                    IsSystemRole = true
                };
                _db.AppRoles.Add(role);
                roles.Add(role);
                rolesAdded++;
            }
            else if (role.IsDeleted)
            {
                role.IsDeleted = false;
                rolesRestored++;
            }

            foreach (var permissionName in DefaultRolePermissions.ForRole(roleName))
            {
                var existingPermission = permissions.SingleOrDefault(permission =>
                    permission.RoleId == role.Id &&
                    permission.Permission == permissionName);

                if (existingPermission is null)
                {
                    var newPermission = new RolePermission
                    {
                        SchoolId = schoolId,
                        RoleId = role.Id,
                        Permission = permissionName
                    };
                    _db.RolePermissions.Add(newPermission);
                    permissions.Add(newPermission);
                    permissionsAdded++;
                }
                else if (existingPermission.IsDeleted)
                {
                    existingPermission.IsDeleted = false;
                    permissionsRestored++;
                }
            }
        }

        await _db.SaveChangesAsync(ct);
        return new SeedResult(rolesAdded, rolesRestored, permissionsAdded, permissionsRestored);
    }
}
