using Asms.Domain.Security;

namespace Asms.Domain.Tests.Security;

public class DefaultRolePermissionsTests
{
    [Fact]
    public void School_roles_has_ten_roles_and_excludes_platform_administrator()
    {
        Assert.Equal(10, DefaultRolePermissions.SchoolRoles.Count);
        Assert.DoesNotContain(
            SystemRoles.PlatformSuperAdministrator,
            DefaultRolePermissions.SchoolRoles);
    }

    [Fact]
    public void ForRole_rejects_platform_and_unknown_roles()
    {
        Assert.Throws<ArgumentException>(() =>
            DefaultRolePermissions.ForRole(SystemRoles.PlatformSuperAdministrator));
        Assert.Throws<ArgumentException>(() =>
            DefaultRolePermissions.ForRole("UnknownRole"));
    }

    [Fact]
    public void Every_role_permission_is_known_and_not_duplicated()
    {
        foreach (var role in DefaultRolePermissions.SchoolRoles)
        {
            var permissions = DefaultRolePermissions.ForRole(role);

            Assert.All(permissions, permission => Assert.Contains(permission, Permissions.All));
            Assert.Equal(permissions.Count, permissions.Distinct(StringComparer.Ordinal).Count());
        }
    }

    [Fact]
    public void Owner_has_all_permissions_and_principal_lacks_only_the_two_manage_permissions()
    {
        Assert.Equal(
            Permissions.All.OrderBy(permission => permission),
            DefaultRolePermissions.ForRole(SystemRoles.SchoolOwner).OrderBy(permission => permission));

        var principal = DefaultRolePermissions.ForRole(SystemRoles.SchoolPrincipal);
        Assert.DoesNotContain(Permissions.Users.Manage, principal);
        Assert.DoesNotContain(Permissions.Settings.Manage, principal);
        Assert.Contains(Permissions.Fees.Reverse, principal);
    }

    [Fact]
    public void Only_owner_and_principal_have_fee_reversal_permission()
    {
        var rolesWithPermission = DefaultRolePermissions.SchoolRoles
            .Where(role => DefaultRolePermissions.ForRole(role).Contains(Permissions.Fees.Reverse))
            .ToArray();

        Assert.Equal(
            new[] { SystemRoles.SchoolOwner, SystemRoles.SchoolPrincipal },
            rolesWithPermission);
    }

    [Fact]
    public void Accountant_has_no_exam_or_attendance_permissions()
    {
        var accountant = DefaultRolePermissions.ForRole(SystemRoles.Accountant);

        Assert.DoesNotContain(accountant, permission => permission.StartsWith("Exams.", StringComparison.Ordinal));
        Assert.DoesNotContain(
            accountant,
            permission => permission.StartsWith("Attendance.", StringComparison.Ordinal));
    }

    [Fact]
    public void Non_owner_and_principal_school_roles_lack_user_and_settings_management()
    {
        var restrictedRoles = new[]
        {
            SystemRoles.Teacher,
            SystemRoles.Parent,
            SystemRoles.Student,
            SystemRoles.Receptionist,
            SystemRoles.Librarian,
            SystemRoles.EducationSupervisor,
            SystemRoles.AcademicManager,
            SystemRoles.Accountant
        };

        foreach (var role in restrictedRoles)
        {
            var permissions = DefaultRolePermissions.ForRole(role);
            Assert.DoesNotContain(Permissions.Users.Manage, permissions);
            Assert.DoesNotContain(Permissions.Settings.Manage, permissions);
        }
    }

    [Fact]
    public void Parent_and_student_lack_student_view_fee_collection_and_audit_view()
    {
        foreach (var role in new[] { SystemRoles.Parent, SystemRoles.Student })
        {
            var permissions = DefaultRolePermissions.ForRole(role);
            Assert.DoesNotContain(Permissions.Students.View, permissions);
            Assert.DoesNotContain(Permissions.Fees.CollectPayment, permissions);
            Assert.DoesNotContain(Permissions.Audit.View, permissions);
        }
    }

    [Fact]
    public void School_owner_and_principal_have_audit_view()
    {
        var rolesWithPermission = DefaultRolePermissions.SchoolRoles
            .Where(role => DefaultRolePermissions.ForRole(role).Contains(Permissions.Audit.View))
            .ToArray();

        Assert.Equal(
            new[] { SystemRoles.SchoolOwner, SystemRoles.SchoolPrincipal },
            rolesWithPermission);
    }

    [Fact]
    public void Changing_a_returned_copy_does_not_change_the_stored_matrix()
    {
        var expected = DefaultRolePermissions.ForRole(SystemRoles.Teacher).ToArray();
        var returnedCopy = DefaultRolePermissions.ForRole(SystemRoles.Teacher);
        var editableCopy = Assert.IsAssignableFrom<IList<string>>(returnedCopy);
        editableCopy[0] = "Changed";

        Assert.Equal(expected, DefaultRolePermissions.ForRole(SystemRoles.Teacher));
    }
}
