using System.Collections.Frozen;
using System.Data.Common;
using Asms.Application.Abstractions;
using Asms.Application.Security;
using Asms.Domain.Academics;
using Asms.Domain.Enums;
using Asms.Domain.Security;
using Asms.Domain.Staffing;
using Asms.Domain.Students;
using Asms.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Asms.Infrastructure.Security;

public sealed class RecordAccessService : IRecordAccessService
{
    private static readonly FrozenSet<string> SchoolWideStudentRoles = new[]
    {
        SystemRoles.SchoolOwner,
        SystemRoles.SchoolPrincipal,
        SystemRoles.AcademicManager,
        SystemRoles.EducationSupervisor,
        SystemRoles.Receptionist,
        SystemRoles.Accountant,
        SystemRoles.Librarian
    }.ToFrozenSet(StringComparer.Ordinal);

    private readonly AsmsDbContext _db;
    private readonly IUserPermissionService _permissions;
    private readonly ICurrentTenant _tenant;
    private readonly object _cacheLock = new();
    private Task<AccessScope>? _cachedScope;

    public RecordAccessService(
        AsmsDbContext db,
        IUserPermissionService permissions,
        ICurrentTenant tenant)
    {
        _db = db;
        _permissions = permissions;
        _tenant = tenant;
    }

    public async Task<bool> CanAccessStudentAsync(Guid studentId, CancellationToken ct = default)
    {
        if (studentId == Guid.Empty)
        {
            return false;
        }

        var scope = await GetScopeAsync(ct);
        return scope.StudentIds.Contains(studentId);
    }

    public async Task<IReadOnlySet<Guid>> GetAccessibleStudentIdsAsync(
        CancellationToken ct = default)
    {
        var scope = await GetScopeAsync(ct);
        return scope.StudentIds;
    }

    public async Task<bool> CanManageClassAsync(
        Guid classRoomId,
        Guid? sectionId,
        Guid? subjectId,
        CancellationToken ct = default)
    {
        if (classRoomId == Guid.Empty)
        {
            return false;
        }

        var scope = await GetScopeAsync(ct);
        return scope.ClassRoomIds.Contains(classRoomId) &&
            (scope.CanManageAllClasses ||
                scope.TeacherAssignments.Any(assignment =>
                    assignment.ClassRoomId == classRoomId &&
                    (assignment.SectionId is null || assignment.SectionId == sectionId) &&
                    (subjectId is null || assignment.SubjectId == subjectId)));
    }

    private Task<AccessScope> GetScopeAsync(CancellationToken ct)
    {
        lock (_cacheLock)
        {
            return _cachedScope ??= LoadScopeAsync(ct);
        }
    }

    private async Task<AccessScope> LoadScopeAsync(CancellationToken ct)
    {
        try
        {
            var access = await _permissions.GetAccessAsync(ct);
            if (!access.IsValid ||
                _tenant.SchoolId is not Guid schoolId ||
                access.AppUserId is not Guid appUserId)
            {
                return AccessScope.Empty;
            }

            var roles = access.Roles.ToFrozenSet(StringComparer.Ordinal);
            var canManageAllClasses =
                roles.Contains(SystemRoles.SchoolOwner) ||
                roles.Contains(SystemRoles.SchoolPrincipal) ||
                roles.Contains(SystemRoles.AcademicManager);
            var hasTeacherRole = roles.Contains(SystemRoles.Teacher);
            var hasParentRole = roles.Contains(SystemRoles.Parent);
            var hasStudentView = access.Has(Permissions.Students.View);
            var studentIds = new HashSet<Guid>();

            if (hasStudentView && roles.Overlaps(SchoolWideStudentRoles))
            {
                var schoolStudentIds = await _db.Students
                    .IgnoreQueryFilters()
                    .AsNoTracking()
                    .Where(student =>
                        student.SchoolId == schoolId &&
                        !student.IsDeleted)
                    .Select(student => student.Id)
                    .ToListAsync(ct);
                studentIds.UnionWith(schoolStudentIds);
            }

            if (hasTeacherRole && hasStudentView)
            {
                var teacherStudentIds = await (
                    from assignment in _db.TeacherAssignments
                        .IgnoreQueryFilters()
                        .AsNoTracking()
                    join teacher in _db.Teachers
                        .IgnoreQueryFilters()
                        .AsNoTracking()
                        on new { assignment.TeacherId, assignment.SchoolId }
                        equals new { TeacherId = teacher.Id, teacher.SchoolId }
                    join classRoom in _db.ClassRooms
                        .IgnoreQueryFilters()
                        .AsNoTracking()
                        on new { assignment.ClassRoomId, assignment.SchoolId }
                        equals new { ClassRoomId = classRoom.Id, classRoom.SchoolId }
                    join academicYear in _db.AcademicYears
                        .IgnoreQueryFilters()
                        .AsNoTracking()
                        on new { assignment.AcademicYearId, assignment.SchoolId }
                        equals new { AcademicYearId = academicYear.Id, academicYear.SchoolId }
                    join enrollment in _db.Enrollments
                        .IgnoreQueryFilters()
                        .AsNoTracking()
                        on new
                        {
                            assignment.ClassRoomId,
                            assignment.AcademicYearId,
                            assignment.SchoolId
                        }
                        equals new
                        {
                            enrollment.ClassRoomId,
                            enrollment.AcademicYearId,
                            enrollment.SchoolId
                        }
                    join student in _db.Students
                        .IgnoreQueryFilters()
                        .AsNoTracking()
                        on new { enrollment.StudentId, enrollment.SchoolId }
                        equals new { StudentId = student.Id, student.SchoolId }
                    where assignment.SchoolId == schoolId &&
                        !assignment.IsDeleted &&
                        teacher.SchoolId == schoolId &&
                        !teacher.IsDeleted &&
                        teacher.AppUserId == appUserId &&
                        classRoom.SchoolId == schoolId &&
                        !classRoom.IsDeleted &&
                        academicYear.SchoolId == schoolId &&
                        !academicYear.IsDeleted &&
                        enrollment.SchoolId == schoolId &&
                        !enrollment.IsDeleted &&
                        enrollment.Status == EnrollmentStatus.Enrolled &&
                        (assignment.SectionId == null ||
                            assignment.SectionId == enrollment.SectionId) &&
                        (assignment.SectionId == null ||
                            _db.Sections.IgnoreQueryFilters().Any(section =>
                                section.Id == assignment.SectionId &&
                                section.SchoolId == schoolId &&
                                !section.IsDeleted)) &&
                        student.SchoolId == schoolId &&
                        !student.IsDeleted
                    select student.Id)
                    .Distinct()
                    .ToListAsync(ct);
                studentIds.UnionWith(teacherStudentIds);
            }

            if (hasParentRole)
            {
                // Parents have no Students.View permission, so their role alone grants linked-child scope.
                var parentStudentIds = await (
                    from link in _db.StudentGuardians
                        .IgnoreQueryFilters()
                        .AsNoTracking()
                    join guardian in _db.Guardians
                        .IgnoreQueryFilters()
                        .AsNoTracking()
                        on new { link.GuardianId, link.SchoolId }
                        equals new { GuardianId = guardian.Id, guardian.SchoolId }
                    join student in _db.Students
                        .IgnoreQueryFilters()
                        .AsNoTracking()
                        on new { link.StudentId, link.SchoolId }
                        equals new { StudentId = student.Id, student.SchoolId }
                    where link.SchoolId == schoolId &&
                        !link.IsDeleted &&
                        guardian.SchoolId == schoolId &&
                        !guardian.IsDeleted &&
                        guardian.AppUserId == appUserId &&
                        student.SchoolId == schoolId &&
                        !student.IsDeleted &&
                        student.Status == StudentStatus.Active
                    select student.Id)
                    .Distinct()
                    .ToListAsync(ct);
                studentIds.UnionWith(parentStudentIds);
            }

            IReadOnlySet<Guid> classRoomIds = Array.Empty<Guid>().ToFrozenSet();
            IReadOnlyList<TeacherAssignmentScope> teacherAssignments =
                Array.Empty<TeacherAssignmentScope>();
            if (canManageAllClasses || hasTeacherRole)
            {
                var validClassRooms = await _db.ClassRooms
                    .IgnoreQueryFilters()
                    .AsNoTracking()
                    .Where(classRoom =>
                        classRoom.SchoolId == schoolId &&
                        !classRoom.IsDeleted)
                    .Select(classRoom => classRoom.Id)
                    .ToListAsync(ct);
                classRoomIds = validClassRooms.ToFrozenSet();
            }

            if (hasTeacherRole && classRoomIds.Count > 0)
            {
                var assignments = await (
                    from assignment in _db.TeacherAssignments
                        .IgnoreQueryFilters()
                        .AsNoTracking()
                    join teacher in _db.Teachers
                        .IgnoreQueryFilters()
                        .AsNoTracking()
                        on new { assignment.TeacherId, assignment.SchoolId }
                        equals new { TeacherId = teacher.Id, teacher.SchoolId }
                    where assignment.SchoolId == schoolId &&
                        !assignment.IsDeleted &&
                        teacher.SchoolId == schoolId &&
                        !teacher.IsDeleted &&
                        teacher.AppUserId == appUserId &&
                        classRoomIds.Contains(assignment.ClassRoomId) &&
                        (assignment.SectionId == null ||
                            _db.Sections.IgnoreQueryFilters().Any(section =>
                                section.Id == assignment.SectionId &&
                                section.SchoolId == schoolId &&
                                !section.IsDeleted)) &&
                        _db.Subjects.IgnoreQueryFilters().Any(subject =>
                            subject.Id == assignment.SubjectId &&
                            subject.SchoolId == schoolId &&
                            !subject.IsDeleted) &&
                        _db.AcademicYears.IgnoreQueryFilters().Any(year =>
                            year.Id == assignment.AcademicYearId &&
                            year.SchoolId == schoolId &&
                            !year.IsDeleted)
                    select new TeacherAssignmentScope(
                        assignment.ClassRoomId,
                        assignment.SectionId,
                        assignment.SubjectId))
                    .Distinct()
                    .ToListAsync(ct);
                teacherAssignments = assignments.AsReadOnly();
            }

            // student self-access comes with the student portal phase
            return new AccessScope(
                studentIds.ToFrozenSet(),
                classRoomIds,
                canManageAllClasses,
                teacherAssignments);
        }
        catch (DbException)
        {
            return AccessScope.Empty;
        }
        catch (InvalidOperationException)
        {
            return AccessScope.Empty;
        }
    }

    private sealed record TeacherAssignmentScope(
        Guid ClassRoomId,
        Guid? SectionId,
        Guid SubjectId);

    private sealed record AccessScope(
        IReadOnlySet<Guid> StudentIds,
        IReadOnlySet<Guid> ClassRoomIds,
        bool CanManageAllClasses,
        IReadOnlyList<TeacherAssignmentScope> TeacherAssignments)
    {
        public static AccessScope Empty { get; } = new(
            Array.Empty<Guid>().ToFrozenSet(),
            Array.Empty<Guid>().ToFrozenSet(),
            false,
            Array.Empty<TeacherAssignmentScope>());
    }
}
