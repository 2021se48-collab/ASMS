namespace Asms.Domain.Security;

/// <summary>
/// This matrix is a starting point; schools may customize it later.
/// Teachers' classes come from TeacherAssignment and parents' children from StudentGuardian in Phase 5.
/// </summary>
public static class DefaultRolePermissions
{
    private static readonly IReadOnlyList<string> SchoolRoleNames = Array.AsReadOnly(new[]
    {
        SystemRoles.SchoolOwner,
        SystemRoles.SchoolPrincipal,
        SystemRoles.AcademicManager,
        SystemRoles.Accountant,
        SystemRoles.Teacher,
        SystemRoles.Receptionist,
        SystemRoles.Librarian,
        SystemRoles.Parent,
        SystemRoles.Student,
        SystemRoles.EducationSupervisor
    });

    private static readonly IReadOnlyDictionary<string, IReadOnlyList<string>> Matrix =
        new Dictionary<string, IReadOnlyList<string>>(StringComparer.Ordinal)
        {
            [SystemRoles.SchoolOwner] = Permissions.All,
            [SystemRoles.SchoolPrincipal] = Permissions.All
                .Where(permission =>
                    permission != Permissions.Users.Manage &&
                    permission != Permissions.Settings.Manage)
                .ToArray(),
            [SystemRoles.AcademicManager] = new[]
            {
                Permissions.Students.View,
                Permissions.Students.Edit,
                Permissions.Teachers.View,
                Permissions.Attendance.View,
                Permissions.Attendance.Edit,
                Permissions.Exams.View,
                Permissions.Exams.EnterMarks,
                Permissions.Exams.Finalize,
                Permissions.Exams.Approve,
                Permissions.Homework.View,
                Permissions.Homework.Manage,
                Permissions.Notifications.View,
                Permissions.Notifications.Send,
                Permissions.Reports.View,
                Permissions.Reports.Export
            },
            [SystemRoles.Accountant] = new[]
            {
                Permissions.Students.View,
                Permissions.Fees.View,
                Permissions.Fees.Invoice,
                Permissions.Fees.CollectPayment,
                Permissions.Finance.ViewReports,
                Permissions.Finance.ManageExpenses,
                Permissions.Finance.ManageSalaries,
                Permissions.Notifications.View,
                Permissions.Reports.View,
                Permissions.Reports.Export
            },
            [SystemRoles.Teacher] = new[]
            {
                Permissions.Students.View,
                Permissions.Attendance.View,
                Permissions.Attendance.Record,
                Permissions.Exams.View,
                Permissions.Exams.EnterMarks,
                Permissions.Homework.View,
                Permissions.Homework.Manage,
                Permissions.Notifications.View,
                Permissions.Notifications.Send
            },
            [SystemRoles.Receptionist] = new[]
            {
                Permissions.Students.View,
                Permissions.Students.Create,
                Permissions.Students.Edit,
                Permissions.Notifications.View
            },
            [SystemRoles.Librarian] = new[]
            {
                Permissions.Library.View,
                Permissions.Library.Manage,
                Permissions.Inventory.View,
                Permissions.Students.View,
                Permissions.Notifications.View
            },
            [SystemRoles.EducationSupervisor] = new[]
            {
                Permissions.Students.View,
                Permissions.Teachers.View,
                Permissions.Attendance.View,
                Permissions.Exams.View,
                Permissions.Reports.View
            },
            [SystemRoles.Parent] = new[]
            {
                Permissions.Attendance.View,
                Permissions.Exams.View,
                Permissions.Fees.View,
                Permissions.Homework.View,
                Permissions.Notifications.View
            },
            [SystemRoles.Student] = new[]
            {
                Permissions.Attendance.View,
                Permissions.Exams.View,
                Permissions.Homework.View,
                Permissions.Notifications.View
            }
        };

    /// <summary>
    /// Gets the roles that schools can assign.
    /// </summary>
    public static IReadOnlyList<string> SchoolRoles => SchoolRoleNames;

    /// <summary>
    /// Gets a copy of the permission list for a school role.
    /// </summary>
    public static IReadOnlyList<string> ForRole(string roleName)
    {
        if (roleName is null ||
            roleName == SystemRoles.PlatformSuperAdministrator ||
            !Matrix.TryGetValue(roleName, out var permissions))
        {
            throw new ArgumentException("The role has no school permission matrix.", nameof(roleName));
        }

        return permissions.ToArray();
    }
}
