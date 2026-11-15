using Asms.Application.Security;
using Asms.Domain.Academics;
using Asms.Domain.Enums;
using Asms.Domain.Security;
using Asms.Domain.Schools;
using Asms.Domain.Staffing;
using Asms.Domain.Students;
using Asms.Infrastructure.Identity;
using Asms.Infrastructure.Persistence;
using Asms.Infrastructure.Persistence.Seeding;
using Asms.Infrastructure.Security;
using Asms.Infrastructure.Tests.Persistence;
using Asms.Infrastructure.Tests.PostgreSql;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Asms.Infrastructure.Tests.Security;

public sealed class RecordAccessServiceTests
{
    [Fact]
    public async Task Teacher_scope_and_class_management_follow_assignments()
    {
        await using var database = await RecordAccessDatabase.CreateSqliteAsync();
        var account = await database.AddAccountAsync(SystemRoles.Teacher);
        var teacher = await database.AddTeacherAsync(account);
        var assignedClass = await database.AddClassAsync();
        var anotherClass = await database.AddClassAsync();
        await database.AddAssignmentAsync(teacher, assignedClass, assignedClass.SectionAId);

        var assignedStudent = await database.AddStudentAsync();
        var unassignedSectionStudent = await database.AddStudentAsync();
        var anotherClassStudent = await database.AddStudentAsync();
        await database.AddEnrollmentAsync(assignedStudent, assignedClass, assignedClass.SectionAId);
        await database.AddEnrollmentAsync(
            unassignedSectionStudent,
            assignedClass,
            assignedClass.SectionBId);
        await database.AddEnrollmentAsync(anotherClassStudent, anotherClass, anotherClass.SectionAId);

        var access = database.CreateRecordAccessService(account);

        Assert.Equal(
            new[] { assignedStudent.Id },
            (await access.GetAccessibleStudentIdsAsync()).OrderBy(id => id));
        Assert.True(await access.CanAccessStudentAsync(assignedStudent.Id));
        Assert.False(await access.CanAccessStudentAsync(unassignedSectionStudent.Id));
        Assert.False(await access.CanAccessStudentAsync(anotherClassStudent.Id));
        Assert.True(await access.CanManageClassAsync(
            assignedClass.ClassRoomId,
            assignedClass.SectionAId,
            assignedClass.SubjectAId));
        Assert.False(await access.CanManageClassAsync(
            anotherClass.ClassRoomId,
            anotherClass.SectionAId,
            anotherClass.SubjectAId));
        Assert.False(await access.CanManageClassAsync(
            assignedClass.ClassRoomId,
            assignedClass.SectionBId,
            assignedClass.SubjectAId));
        Assert.False(await access.CanManageClassAsync(
            assignedClass.ClassRoomId,
            assignedClass.SectionAId,
            assignedClass.SubjectBId));
        Assert.True(await access.CanManageClassAsync(
            assignedClass.ClassRoomId,
            assignedClass.SectionAId,
            null));
    }

    [Fact]
    public async Task Teacher_scope_ignores_deleted_assignments_enrollments_and_non_enrolled_students()
    {
        await using var database = await RecordAccessDatabase.CreateSqliteAsync();
        var account = await database.AddAccountAsync(SystemRoles.Teacher);
        var teacher = await database.AddTeacherAsync(account);
        var classRoom = await database.AddClassAsync();
        var assignedSection = await database.AddSectionAsync(classRoom);
        var activeStudent = await database.AddStudentAsync();
        var deletedEnrollmentStudent = await database.AddStudentAsync();
        var transferredStudent = await database.AddStudentAsync();
        var withdrawnStudent = await database.AddStudentAsync();
        await database.AddEnrollmentAsync(activeStudent, classRoom, assignedSection.Id);
        await database.AddEnrollmentAsync(
            deletedEnrollmentStudent,
            classRoom,
            assignedSection.Id,
            isDeleted: true);
        await database.AddEnrollmentAsync(
            transferredStudent,
            classRoom,
            assignedSection.Id,
            EnrollmentStatus.Transferred);
        await database.AddEnrollmentAsync(
            withdrawnStudent,
            classRoom,
            assignedSection.Id,
            EnrollmentStatus.Withdrawn);
        var deletedAssignment = await database.AddAssignmentAsync(
            teacher,
            classRoom,
            assignedSection.Id,
            isDeleted: true);

        var noLiveAssignmentAccess = database.CreateRecordAccessService(account);
        Assert.Empty(await noLiveAssignmentAccess.GetAccessibleStudentIdsAsync());
        Assert.False(await noLiveAssignmentAccess.CanManageClassAsync(
            classRoom.ClassRoomId,
            assignedSection.Id,
            classRoom.SubjectAId));

        deletedAssignment.IsDeleted = false;
        await database.Db.SaveChangesAsync();
        var service = database.CreateRecordAccessService(account);
        Assert.Equal(
            new[] { activeStudent.Id },
            (await service.GetAccessibleStudentIdsAsync()).OrderBy(id => id));
        Assert.False(await service.CanAccessStudentAsync(deletedEnrollmentStudent.Id));
        Assert.False(await service.CanAccessStudentAsync(transferredStudent.Id));
        Assert.False(await service.CanAccessStudentAsync(withdrawnStudent.Id));
    }

    [Fact]
    public async Task Teacher_assignment_without_a_section_covers_the_whole_class()
    {
        await using var database = await RecordAccessDatabase.CreateSqliteAsync();
        var account = await database.AddAccountAsync(SystemRoles.Teacher);
        var teacher = await database.AddTeacherAsync(account);
        var classRoom = await database.AddClassAsync();
        await database.AddAssignmentAsync(teacher, classRoom, sectionId: null);
        var sectionAStudent = await database.AddStudentAsync();
        var sectionBStudent = await database.AddStudentAsync();
        await database.AddEnrollmentAsync(sectionAStudent, classRoom, classRoom.SectionAId);
        await database.AddEnrollmentAsync(sectionBStudent, classRoom, classRoom.SectionBId);

        var access = database.CreateRecordAccessService(account);

        Assert.Equal(
            new[] { sectionAStudent.Id, sectionBStudent.Id }.OrderBy(id => id),
            (await access.GetAccessibleStudentIdsAsync()).OrderBy(id => id));
        Assert.True(await access.CanManageClassAsync(
            classRoom.ClassRoomId,
            classRoom.SectionBId,
            classRoom.SubjectAId));
    }

    [Fact]
    public async Task Teacher_without_a_teacher_record_or_assignment_gets_no_scope()
    {
        await using var database = await RecordAccessDatabase.CreateSqliteAsync();
        var noTeacher = await database.AddAccountAsync(SystemRoles.Teacher);
        var noAssignment = await database.AddAccountAsync(SystemRoles.Teacher);
        await database.AddTeacherAsync(noAssignment);
        var classRoom = await database.AddClassAsync();
        var student = await database.AddStudentAsync();
        await database.AddEnrollmentAsync(student, classRoom, classRoom.SectionAId);

        var noTeacherAccess = database.CreateRecordAccessService(noTeacher);
        var noAssignmentAccess = database.CreateRecordAccessService(noAssignment);

        Assert.Empty(await noTeacherAccess.GetAccessibleStudentIdsAsync());
        Assert.False(await noTeacherAccess.CanManageClassAsync(
            classRoom.ClassRoomId,
            classRoom.SectionAId,
            classRoom.SubjectAId));
        Assert.Empty(await noAssignmentAccess.GetAccessibleStudentIdsAsync());
        Assert.False(await noAssignmentAccess.CanManageClassAsync(
            classRoom.ClassRoomId,
            classRoom.SectionAId,
            classRoom.SubjectAId));
    }

    [Fact]
    public async Task Parent_scope_contains_only_active_linked_children()
    {
        await using var database = await RecordAccessDatabase.CreateSqliteAsync();
        var parent = await database.AddAccountAsync(SystemRoles.Parent);
        var otherParent = await database.AddAccountAsync(SystemRoles.Parent);
        var parentGuardian = await database.AddGuardianAsync(parent);
        var otherGuardian = await database.AddGuardianAsync(otherParent);
        var classRoom = await database.AddClassAsync();
        var deletedGuardian = await database.AddGuardianAsync(parent);
        deletedGuardian.IsDeleted = true;
        await database.Db.SaveChangesAsync();
        var firstChild = await database.AddStudentAsync();
        var secondChild = await database.AddStudentAsync();
        var otherGuardianChild = await database.AddStudentAsync();
        var removedLinkChild = await database.AddStudentAsync();
        var removedGuardianChild = await database.AddStudentAsync();
        var withdrawnChild = await database.AddStudentAsync(StudentStatus.Withdrawn);
        await database.AddStudentGuardianAsync(firstChild, parentGuardian);
        await database.AddStudentGuardianAsync(secondChild, parentGuardian);
        await database.AddStudentGuardianAsync(otherGuardianChild, otherGuardian);
        await database.AddStudentGuardianAsync(removedLinkChild, parentGuardian, isDeleted: true);
        await database.AddStudentGuardianAsync(
            removedGuardianChild,
            deletedGuardian);
        await database.AddStudentGuardianAsync(withdrawnChild, parentGuardian);

        var initialAccess = database.CreateRecordAccessService(parent);
        Assert.Equal(
            new[] { firstChild.Id, secondChild.Id }.OrderBy(id => id),
            (await initialAccess.GetAccessibleStudentIdsAsync()).OrderBy(id => id));
        Assert.False(await initialAccess.CanAccessStudentAsync(otherGuardianChild.Id));
        Assert.False(await initialAccess.CanAccessStudentAsync(removedLinkChild.Id));
        Assert.False(await initialAccess.CanAccessStudentAsync(removedGuardianChild.Id));
        Assert.False(await initialAccess.CanAccessStudentAsync(withdrawnChild.Id));
        Assert.False(await initialAccess.CanManageClassAsync(classRoom.ClassRoomId, null, null));
    }

    [Fact]
    public async Task Parent_without_a_guardian_login_link_gets_no_children()
    {
        await using var database = await RecordAccessDatabase.CreateSqliteAsync();
        var parent = await database.AddAccountAsync(SystemRoles.Parent);
        var child = await database.AddStudentAsync();
        var guardianWithoutLogin = await database.AddGuardianAsync(appUserId: null);
        await database.AddStudentGuardianAsync(child, guardianWithoutLogin);

        var access = database.CreateRecordAccessService(parent);

        Assert.Empty(await access.GetAccessibleStudentIdsAsync());
        Assert.False(await access.CanAccessStudentAsync(child.Id));
    }

    [Fact]
    public async Task School_wide_roles_see_school_students_but_only_leaders_manage_classes()
    {
        await using var database = await RecordAccessDatabase.CreateSqliteAsync();
        var principal = await database.AddAccountAsync(SystemRoles.SchoolPrincipal);
        var accountant = await database.AddAccountAsync(SystemRoles.Accountant);
        var owner = await database.AddAccountAsync(SystemRoles.SchoolOwner);
        var academicManager = await database.AddAccountAsync(SystemRoles.AcademicManager);
        var classRoom = await database.AddClassAsync();
        var firstStudent = await database.AddStudentAsync();
        var secondStudent = await database.AddStudentAsync(StudentStatus.Withdrawn);
        var deletedStudent = await database.AddStudentAsync(isDeleted: true);

        var principalAccess = database.CreateRecordAccessService(principal);
        var principalStudents = await principalAccess.GetAccessibleStudentIdsAsync();
        var accountantAccess = database.CreateRecordAccessService(accountant);
        var accountantStudents = await accountantAccess.GetAccessibleStudentIdsAsync();
        var ownerAccess = database.CreateRecordAccessService(owner);
        var academicManagerAccess = database.CreateRecordAccessService(academicManager);

        var expectedStudents = new[] { firstStudent.Id, secondStudent.Id }.OrderBy(id => id);
        Assert.Equal(expectedStudents, principalStudents.OrderBy(id => id));
        Assert.Equal(expectedStudents, accountantStudents.OrderBy(id => id));
        Assert.False(await principalAccess.CanAccessStudentAsync(deletedStudent.Id));
        Assert.True(await principalAccess.CanManageClassAsync(classRoom.ClassRoomId, null, null));
        Assert.True(await ownerAccess.CanManageClassAsync(classRoom.ClassRoomId, null, null));
        Assert.True(await academicManagerAccess.CanManageClassAsync(classRoom.ClassRoomId, null, null));
        Assert.False(await accountantAccess.CanManageClassAsync(classRoom.ClassRoomId, null, null));
    }

    [Fact]
    public async Task Teacher_and_parent_roles_union_their_student_scopes()
    {
        await using var database = await RecordAccessDatabase.CreateSqliteAsync();
        var account = await database.AddAccountAsync(SystemRoles.Teacher, SystemRoles.Parent);
        var teacher = await database.AddTeacherAsync(account);
        var guardian = await database.AddGuardianAsync(account);
        var classRoom = await database.AddClassAsync();
        await database.AddAssignmentAsync(teacher, classRoom, classRoom.SectionAId);
        var enrolledStudent = await database.AddStudentAsync();
        var linkedStudent = await database.AddStudentAsync();
        await database.AddEnrollmentAsync(enrolledStudent, classRoom, classRoom.SectionAId);
        await database.AddStudentGuardianAsync(linkedStudent, guardian);

        var access = database.CreateRecordAccessService(account);

        Assert.Equal(
            new[] { enrolledStudent.Id, linkedStudent.Id }.OrderBy(id => id),
            (await access.GetAccessibleStudentIdsAsync()).OrderBy(id => id));
    }

    [Fact]
    public async Task Cross_school_guessed_ids_random_ids_and_deleted_students_are_denied()
    {
        await using var database = await RecordAccessDatabase.CreateSqliteAsync();
        var teacherAccount = await database.AddAccountAsync(SystemRoles.Teacher);
        var teacher = await database.AddTeacherAsync(teacherAccount);
        var parentAccount = await database.AddAccountAsync(SystemRoles.Parent);
        var parentGuardian = await database.AddGuardianAsync(parentAccount);
        var classroom = await database.AddClassAsync();
        await database.AddAssignmentAsync(teacher, classroom, classroom.SectionAId);
        var teacherStudent = await database.AddStudentAsync();
        var deletedStudent = await database.AddStudentAsync(isDeleted: true);
        await database.AddEnrollmentAsync(teacherStudent, classroom, classroom.SectionAId);
        await database.AddStudentGuardianAsync(teacherStudent, parentGuardian);
        var schoolBChild = await database.AddOtherSchoolStudentAsync(parentAccount.AppUserId);

        var teacherAccess = database.CreateRecordAccessService(teacherAccount);
        var parentAccess = database.CreateRecordAccessService(parentAccount);

        Assert.False(await teacherAccess.CanAccessStudentAsync(schoolBChild.Id));
        Assert.False(await parentAccess.CanAccessStudentAsync(schoolBChild.Id));
        Assert.DoesNotContain(schoolBChild.Id, await teacherAccess.GetAccessibleStudentIdsAsync());
        Assert.DoesNotContain(schoolBChild.Id, await parentAccess.GetAccessibleStudentIdsAsync());
        Assert.False(await teacherAccess.CanAccessStudentAsync(Guid.NewGuid()));
        Assert.False(await parentAccess.CanAccessStudentAsync(Guid.NewGuid()));
        Assert.False(await teacherAccess.CanAccessStudentAsync(deletedStudent.Id));
    }

    [Fact]
    public async Task No_roles_and_invalid_access_return_empty_scope_and_false()
    {
        await using var database = await RecordAccessDatabase.CreateSqliteAsync();
        var noRoles = await database.AddAccountAsync();
        var studentRole = await database.AddAccountAsync(SystemRoles.Student);
        var teacher = await database.AddAccountAsync(SystemRoles.Teacher);
        var teacherRecord = await database.AddTeacherAsync(teacher);
        var classRoom = await database.AddClassAsync();
        await database.AddAssignmentAsync(teacherRecord, classRoom, classRoom.SectionAId);
        var student = await database.AddStudentAsync();
        await database.AddEnrollmentAsync(student, classRoom, classRoom.SectionAId);
        var deniedService = database.CreateRecordAccessService(noRoles);

        Assert.Empty(await deniedService.GetAccessibleStudentIdsAsync());
        Assert.False(await deniedService.CanAccessStudentAsync(student.Id));
        Assert.False(await deniedService.CanManageClassAsync(classRoom.ClassRoomId, null, null));

        var studentRoleAccess = database.CreateRecordAccessService(studentRole);
        Assert.Empty(await studentRoleAccess.GetAccessibleStudentIdsAsync());
        Assert.False(await studentRoleAccess.CanAccessStudentAsync(student.Id));
        Assert.False(await studentRoleAccess.CanManageClassAsync(classRoom.ClassRoomId, null, null));

        var noTenantUser = database.CreateRecordAccessService(teacher, includeUser: false);
        Assert.Empty(await noTenantUser.GetAccessibleStudentIdsAsync());
        Assert.False(await noTenantUser.CanAccessStudentAsync(student.Id));
        Assert.False(await noTenantUser.CanManageClassAsync(classRoom.ClassRoomId, null, null));

        var noTenantSchool = new RecordAccessService(
            database.Db,
            new FixedPermissionService(new UserAccess(
                true,
                [SystemRoles.SchoolPrincipal],
                new HashSet<string>([Permissions.Students.View], StringComparer.Ordinal),
                teacher.AppUserId)),
            new TestTenant { UserId = teacher.IdentityUserId });
        Assert.Empty(await noTenantSchool.GetAccessibleStudentIdsAsync());
        Assert.False(await noTenantSchool.CanAccessStudentAsync(student.Id));
        Assert.False(await noTenantSchool.CanManageClassAsync(classRoom.ClassRoomId, null, null));
    }

    [Fact]
    public async Task Scope_is_cached_for_the_service_instance()
    {
        await using var database = await RecordAccessDatabase.CreateSqliteAsync();
        var account = await database.AddAccountAsync(SystemRoles.Teacher);
        var teacher = await database.AddTeacherAsync(account);
        var classRoom = await database.AddClassAsync();
        await database.AddAssignmentAsync(teacher, classRoom, classRoom.SectionAId);
        var student = await database.AddStudentAsync();
        var access = database.CreateRecordAccessService(account);

        Assert.Empty(await access.GetAccessibleStudentIdsAsync());
        await database.AddEnrollmentAsync(student, classRoom, classRoom.SectionAId);
        Assert.Empty(await access.GetAccessibleStudentIdsAsync());
        Assert.Equal(
            new[] { student.Id },
            (await database.CreateRecordAccessService(account).GetAccessibleStudentIdsAsync())
                .OrderBy(id => id));
    }

    [Fact]
    public async Task Disposed_database_context_fails_closed_without_throwing()
    {
        await using var database = await RecordAccessDatabase.CreateSqliteAsync();
        var account = await database.AddAccountAsync(SystemRoles.Teacher);
        var disposedDb = database.CreateDomainContext(database.SchoolId);
        await disposedDb.DisposeAsync();
        var permissions = new FixedPermissionService(new UserAccess(
            true,
            [SystemRoles.SchoolPrincipal],
            new HashSet<string>([Permissions.Students.View], StringComparer.Ordinal),
            account.AppUserId));
        var service = new RecordAccessService(disposedDb, permissions, database.Tenant);

        Assert.Empty(await service.GetAccessibleStudentIdsAsync());
        Assert.False(await service.CanAccessStudentAsync(Guid.NewGuid()));
        Assert.False(await service.CanManageClassAsync(Guid.NewGuid(), null, null));
    }

    [PostgresFact]
    public async Task PostgreSql_teacher_scope_includes_only_enrolled_students_in_assignment()
    {
        await using var database = await PostgresTestDatabase.CreateAsync();
        var schoolId = Guid.NewGuid();
        var tenant = new TestTenant { SchoolId = schoolId };
        await using var db = database.CreateContext(tenant);
        var identityOptions = new DbContextOptionsBuilder<AsmsIdentityDbContext>();
        database.ConfigureIdentityDbContext(identityOptions);
        await using var identityDb = new AsmsIdentityDbContext(identityOptions.Options);
        await db.Database.MigrateAsync();
        await identityDb.Database.MigrateAsync();
        var fixture = new PostgreSqlRecordAccessFixture(db, identityDb, tenant, schoolId);
        await fixture.SeedAsync();
        var account = await fixture.AddAccountAsync(SystemRoles.Teacher);
        var teacher = await fixture.AddTeacherAsync(account);
        var classRoom = await fixture.AddClassAsync();
        await fixture.AddAssignmentAsync(teacher, classRoom, classRoom.SectionAId);
        var assignedStudent = await fixture.AddStudentAsync();
        var otherStudent = await fixture.AddStudentAsync();
        await fixture.AddEnrollmentAsync(assignedStudent, classRoom, classRoom.SectionAId);
        await fixture.AddEnrollmentAsync(otherStudent, classRoom, classRoom.SectionBId);

        var access = fixture.CreateRecordAccessService(account);

        Assert.Equal([assignedStudent.Id], await access.GetAccessibleStudentIdsAsync());
        Assert.False(await access.CanAccessStudentAsync(otherStudent.Id));
    }

    [PostgresFact]
    public async Task PostgreSql_parent_scope_includes_only_active_linked_children()
    {
        await using var database = await PostgresTestDatabase.CreateAsync();
        var schoolId = Guid.NewGuid();
        var tenant = new TestTenant { SchoolId = schoolId };
        await using var db = database.CreateContext(tenant);
        var identityOptions = new DbContextOptionsBuilder<AsmsIdentityDbContext>();
        database.ConfigureIdentityDbContext(identityOptions);
        await using var identityDb = new AsmsIdentityDbContext(identityOptions.Options);
        await db.Database.MigrateAsync();
        await identityDb.Database.MigrateAsync();
        var fixture = new PostgreSqlRecordAccessFixture(db, identityDb, tenant, schoolId);
        await fixture.SeedAsync();
        var account = await fixture.AddAccountAsync(SystemRoles.Parent);
        var guardian = await fixture.AddGuardianAsync(account);
        var child = await fixture.AddStudentAsync();
        var unlinked = await fixture.AddStudentAsync();
        await fixture.AddStudentGuardianAsync(child, guardian);

        var access = fixture.CreateRecordAccessService(account);

        Assert.Equal([child.Id], await access.GetAccessibleStudentIdsAsync());
        Assert.False(await access.CanAccessStudentAsync(unlinked.Id));
    }

    private sealed record SeededAccount(Guid IdentityUserId, Guid AppUserId);

    private sealed record ClassData(
        Guid ClassRoomId,
        Guid AcademicYearId,
        Guid SectionAId,
        Guid SectionBId,
        Guid SubjectAId,
        Guid SubjectBId);

    private sealed class RecordAccessDatabase : IAsyncDisposable
    {
        private readonly SqliteConnection _connection;
        private readonly DbContextOptions<AsmsDbContext> _dbOptions;
        private readonly DbContextOptions<AsmsIdentityDbContext> _identityOptions;

        private RecordAccessDatabase()
        {
            _connection = new SqliteConnection("Data Source=:memory:");
            _connection.Open();
            SchoolId = Guid.NewGuid();
            Tenant = new TestTenant { SchoolId = SchoolId };
            _dbOptions = new DbContextOptionsBuilder<AsmsDbContext>()
                .UseSqlite(
                    _connection,
                    sqlite => sqlite.MigrationsAssembly("Asms.Infrastructure.Migrations.Sqlite"))
                .Options;
            _identityOptions = new DbContextOptionsBuilder<AsmsIdentityDbContext>()
                .UseSqlite(
                    _connection,
                    sqlite => sqlite.MigrationsAssembly("Asms.Infrastructure.Migrations.Sqlite"))
                .Options;
            Db = new AsmsDbContext(_dbOptions, Tenant);
            IdentityDb = new AsmsIdentityDbContext(_identityOptions);
        }

        public Guid SchoolId { get; }

        public TestTenant Tenant { get; }

        public AsmsDbContext Db { get; }

        private AsmsIdentityDbContext IdentityDb { get; }

        public static async Task<RecordAccessDatabase> CreateSqliteAsync()
        {
            var database = new RecordAccessDatabase();
            await database.Db.Database.MigrateAsync();
            await database.IdentityDb.Database.MigrateAsync();
            database.Db.Schools.Add(new School
            {
                Id = database.SchoolId,
                Name = $"School {database.SchoolId:N}"
            });
            await database.Db.SaveChangesAsync();
            await new SchoolRoleSeeder(database.Db, database.Tenant)
                .SeedAsync(database.SchoolId);
            return database;
        }

        public async Task<SeededAccount> AddAccountAsync(params string[] roles)
        {
            var appUserId = Guid.NewGuid();
            Db.AppUsers.Add(new AppUser
            {
                Id = appUserId,
                SchoolId = SchoolId,
                FullName = "Test account"
            });
            foreach (var roleName in roles)
            {
                var role = await Db.AppRoles.SingleAsync(item => item.Name == roleName);
                Db.UserRoles.Add(new UserRole
                {
                    SchoolId = SchoolId,
                    UserId = appUserId,
                    RoleId = role.Id
                });
            }

            await Db.SaveChangesAsync();
            var identityUserId = Guid.NewGuid();
            IdentityDb.Users.Add(new AsmsIdentityUser
            {
                Id = identityUserId,
                UserName = $"user-{identityUserId:N}",
                NormalizedUserName = $"USER-{identityUserId:N}",
                SchoolId = SchoolId,
                AppUserId = appUserId
            });
            await IdentityDb.SaveChangesAsync();
            return new SeededAccount(identityUserId, appUserId);
        }

        public async Task<Teacher> AddTeacherAsync(SeededAccount account)
        {
            var teacher = new Teacher
            {
                SchoolId = SchoolId,
                EmployeeNumber = $"T-{Guid.NewGuid():N}",
                FirstName = "Test",
                LastName = "Teacher",
                HireDate = new DateOnly(2020, 1, 1),
                AppUserId = account.AppUserId
            };
            Db.Teachers.Add(teacher);
            await Db.SaveChangesAsync();
            return teacher;
        }

        public async Task<Guardian> AddGuardianAsync(
            SeededAccount? account = null,
            Guid? appUserId = null)
        {
            var guardian = new Guardian
            {
                SchoolId = SchoolId,
                FullName = "Test guardian",
                AppUserId = appUserId ?? account?.AppUserId
            };
            Db.Guardians.Add(guardian);
            await Db.SaveChangesAsync();
            return guardian;
        }

        public async Task<ClassData> AddClassAsync()
        {
            var suffix = Guid.NewGuid().ToString("N");
            var year = new AcademicYear
            {
                SchoolId = SchoolId,
                Name = $"Year {suffix}",
                StartDate = new DateOnly(2025, 1, 1),
                EndDate = new DateOnly(2025, 12, 31)
            };
            var grade = new Grade { SchoolId = SchoolId, Name = $"Grade {suffix}" };
            Db.AddRange(year, grade);
            await Db.SaveChangesAsync();
            var classRoom = new ClassRoom
            {
                SchoolId = SchoolId,
                GradeId = grade.Id,
                AcademicYearId = year.Id,
                Name = $"Class {suffix}"
            };
            var subjectA = new Subject { SchoolId = SchoolId, Name = $"Subject A {suffix}" };
            var subjectB = new Subject { SchoolId = SchoolId, Name = $"Subject B {suffix}" };
            Db.AddRange(classRoom, subjectA, subjectB);
            await Db.SaveChangesAsync();
            var sectionA = new Section
            {
                SchoolId = SchoolId,
                ClassRoomId = classRoom.Id,
                Name = "A"
            };
            var sectionB = new Section
            {
                SchoolId = SchoolId,
                ClassRoomId = classRoom.Id,
                Name = "B"
            };
            Db.AddRange(sectionA, sectionB);
            await Db.SaveChangesAsync();
            return new ClassData(
                classRoom.Id,
                year.Id,
                sectionA.Id,
                sectionB.Id,
                subjectA.Id,
                subjectB.Id);
        }

        public async Task<Section> AddSectionAsync(ClassData classRoom)
        {
            var section = new Section
            {
                SchoolId = SchoolId,
                ClassRoomId = classRoom.ClassRoomId,
                Name = $"Section {Guid.NewGuid():N}"
            };
            Db.Sections.Add(section);
            await Db.SaveChangesAsync();
            return section;
        }

        public async Task<Student> AddStudentAsync(
            StudentStatus status = StudentStatus.Active,
            bool isDeleted = false)
        {
            var suffix = Guid.NewGuid().ToString("N");
            var student = new Student
            {
                SchoolId = SchoolId,
                StudentNumber = $"S-{suffix}",
                FirstName = "Test",
                LastName = "Student",
                FatherName = "Parent",
                DateOfBirth = new DateOnly(2015, 1, 1),
                AdmissionDate = new DateOnly(2024, 1, 1),
                Status = status,
                IsDeleted = isDeleted,
                DuplicateKey = suffix
            };
            Db.Students.Add(student);
            await Db.SaveChangesAsync();
            return student;
        }

        public async Task<Enrollment> AddEnrollmentAsync(
            Student student,
            ClassData classRoom,
            Guid? sectionId,
            EnrollmentStatus status = EnrollmentStatus.Enrolled,
            bool isDeleted = false)
        {
            var enrollment = new Enrollment
            {
                SchoolId = SchoolId,
                StudentId = student.Id,
                AcademicYearId = classRoom.AcademicYearId,
                ClassRoomId = classRoom.ClassRoomId,
                SectionId = sectionId,
                EnrollmentDate = new DateOnly(2025, 1, 1),
                Status = status,
                IsDeleted = isDeleted
            };
            Db.Enrollments.Add(enrollment);
            await Db.SaveChangesAsync();
            return enrollment;
        }

        public async Task<TeacherAssignment> AddAssignmentAsync(
            Teacher teacher,
            ClassData classRoom,
            Guid? sectionId,
            bool isDeleted = false)
        {
            var assignment = new TeacherAssignment
            {
                SchoolId = SchoolId,
                TeacherId = teacher.Id,
                SubjectId = classRoom.SubjectAId,
                ClassRoomId = classRoom.ClassRoomId,
                SectionId = sectionId,
                AcademicYearId = classRoom.AcademicYearId,
                IsDeleted = isDeleted
            };
            Db.TeacherAssignments.Add(assignment);
            await Db.SaveChangesAsync();
            return assignment;
        }

        public async Task<StudentGuardian> AddStudentGuardianAsync(
            Student student,
            Guardian guardian,
            bool isDeleted = false)
        {
            var link = new StudentGuardian
            {
                SchoolId = SchoolId,
                StudentId = student.Id,
                GuardianId = guardian.Id,
                IsDeleted = isDeleted
            };
            Db.StudentGuardians.Add(link);
            await Db.SaveChangesAsync();
            return link;
        }

        public async Task<Student> AddOtherSchoolStudentAsync(Guid appUserId)
        {
            var schoolId = Guid.NewGuid();
            await using var otherDb = CreateDomainContext(schoolId);
            otherDb.Schools.Add(new School
            {
                Id = schoolId,
                Name = $"School {schoolId:N}"
            });
            await otherDb.SaveChangesAsync();
            var suffix = Guid.NewGuid().ToString("N");
            var year = new AcademicYear
            {
                SchoolId = schoolId,
                Name = $"Year {suffix}",
                StartDate = new DateOnly(2025, 1, 1),
                EndDate = new DateOnly(2025, 12, 31)
            };
            var grade = new Grade { SchoolId = schoolId, Name = $"Grade {suffix}" };
            otherDb.AddRange(year, grade);
            await otherDb.SaveChangesAsync();
            var classRoom = new ClassRoom
            {
                SchoolId = schoolId,
                GradeId = grade.Id,
                AcademicYearId = year.Id,
                Name = $"Class {suffix}"
            };
            var student = new Student
            {
                SchoolId = schoolId,
                StudentNumber = $"S-{suffix}",
                FirstName = "Other",
                LastName = "School",
                FatherName = "Parent",
                DateOfBirth = new DateOnly(2015, 1, 1),
                AdmissionDate = new DateOnly(2024, 1, 1),
                DuplicateKey = suffix
            };
            otherDb.AddRange(classRoom, student);
            await otherDb.SaveChangesAsync();
            var guardian = new Guardian
            {
                SchoolId = schoolId,
                FullName = "School B guardian",
                AppUserId = appUserId
            };
            otherDb.Guardians.Add(guardian);
            await otherDb.SaveChangesAsync();
            otherDb.StudentGuardians.Add(new StudentGuardian
            {
                SchoolId = schoolId,
                StudentId = student.Id,
                GuardianId = guardian.Id
            });
            await otherDb.SaveChangesAsync();
            return student;
        }

        public RecordAccessService CreateRecordAccessService(SeededAccount account)
            => CreateRecordAccessService(account, includeUser: true);

        public RecordAccessService CreateRecordAccessService(
            SeededAccount account,
            bool includeUser)
        {
            var serviceTenant = new TestTenant
            {
                SchoolId = SchoolId,
                UserId = includeUser ? account.IdentityUserId : null
            };
            return new RecordAccessService(
                Db,
                new UserPermissionService(Db, IdentityDb, serviceTenant),
                serviceTenant);
        }

        public AsmsDbContext CreateDomainContext(Guid schoolId) =>
            new(_dbOptions, new TestTenant { SchoolId = schoolId });

        public async ValueTask DisposeAsync()
        {
            await Db.DisposeAsync();
            await IdentityDb.DisposeAsync();
            await _connection.DisposeAsync();
        }
    }

    private sealed class PostgreSqlRecordAccessFixture
    {
        private readonly AsmsDbContext _db;
        private readonly AsmsIdentityDbContext _identityDb;
        private readonly TestTenant _tenant;
        private readonly Guid _schoolId;

        public PostgreSqlRecordAccessFixture(
            AsmsDbContext db,
            AsmsIdentityDbContext identityDb,
            TestTenant tenant,
            Guid schoolId)
        {
            _db = db;
            _identityDb = identityDb;
            _tenant = tenant;
            _schoolId = schoolId;
        }

        public async Task SeedAsync()
        {
            _db.Schools.Add(new School
            {
                Id = _schoolId,
                Name = $"School {_schoolId:N}"
            });
            await _db.SaveChangesAsync();
            await new SchoolRoleSeeder(_db, _tenant).SeedAsync(_schoolId);
        }

        public async Task<SeededAccount> AddAccountAsync(params string[] roles)
        {
            var appUserId = Guid.NewGuid();
            _db.AppUsers.Add(new AppUser { Id = appUserId, SchoolId = _schoolId, FullName = "Test" });
            foreach (var roleName in roles)
            {
                var role = await _db.AppRoles.SingleAsync(item => item.Name == roleName);
                _db.UserRoles.Add(new UserRole
                {
                    SchoolId = _schoolId,
                    UserId = appUserId,
                    RoleId = role.Id
                });
            }

            await _db.SaveChangesAsync();
            var identityUserId = Guid.NewGuid();
            _identityDb.Users.Add(new AsmsIdentityUser
            {
                Id = identityUserId,
                UserName = $"user-{identityUserId:N}",
                NormalizedUserName = $"USER-{identityUserId:N}",
                SchoolId = _schoolId,
                AppUserId = appUserId
            });
            await _identityDb.SaveChangesAsync();
            _tenant.UserId = identityUserId;
            return new SeededAccount(identityUserId, appUserId);
        }

        public async Task<Teacher> AddTeacherAsync(SeededAccount account)
        {
            var teacher = new Teacher
            {
                SchoolId = _schoolId,
                EmployeeNumber = $"T-{Guid.NewGuid():N}",
                FirstName = "Test",
                LastName = "Teacher",
                HireDate = new DateOnly(2020, 1, 1),
                AppUserId = account.AppUserId
            };
            _db.Teachers.Add(teacher);
            await _db.SaveChangesAsync();
            return teacher;
        }

        public async Task<Guardian> AddGuardianAsync(SeededAccount account)
        {
            var guardian = new Guardian
            {
                SchoolId = _schoolId,
                FullName = "Test guardian",
                AppUserId = account.AppUserId
            };
            _db.Guardians.Add(guardian);
            await _db.SaveChangesAsync();
            return guardian;
        }

        public async Task<ClassData> AddClassAsync()
        {
            var suffix = Guid.NewGuid().ToString("N");
            var year = new AcademicYear
            {
                SchoolId = _schoolId,
                Name = $"Year {suffix}",
                StartDate = new DateOnly(2025, 1, 1),
                EndDate = new DateOnly(2025, 12, 31)
            };
            var grade = new Grade { SchoolId = _schoolId, Name = $"Grade {suffix}" };
            _db.AddRange(year, grade);
            await _db.SaveChangesAsync();
            var classRoom = new ClassRoom
            {
                SchoolId = _schoolId,
                GradeId = grade.Id,
                AcademicYearId = year.Id,
                Name = $"Class {suffix}"
            };
            var subject = new Subject { SchoolId = _schoolId, Name = $"Subject {suffix}" };
            _db.AddRange(classRoom, subject);
            await _db.SaveChangesAsync();
            var sectionA = new Section
            {
                SchoolId = _schoolId,
                ClassRoomId = classRoom.Id,
                Name = "A"
            };
            var sectionB = new Section
            {
                SchoolId = _schoolId,
                ClassRoomId = classRoom.Id,
                Name = "B"
            };
            _db.AddRange(sectionA, sectionB);
            await _db.SaveChangesAsync();
            return new ClassData(
                classRoom.Id,
                year.Id,
                sectionA.Id,
                sectionB.Id,
                subject.Id,
                subject.Id);
        }

        public async Task AddAssignmentAsync(
            Teacher teacher,
            ClassData classRoom,
            Guid? sectionId)
        {
            _db.TeacherAssignments.Add(new TeacherAssignment
            {
                SchoolId = _schoolId,
                TeacherId = teacher.Id,
                SubjectId = classRoom.SubjectAId,
                ClassRoomId = classRoom.ClassRoomId,
                SectionId = sectionId,
                AcademicYearId = classRoom.AcademicYearId
            });
            await _db.SaveChangesAsync();
        }

        public async Task<Student> AddStudentAsync()
        {
            var suffix = Guid.NewGuid().ToString("N");
            var student = new Student
            {
                SchoolId = _schoolId,
                StudentNumber = $"S-{suffix}",
                FirstName = "Test",
                LastName = "Student",
                FatherName = "Parent",
                DateOfBirth = new DateOnly(2015, 1, 1),
                AdmissionDate = new DateOnly(2024, 1, 1),
                DuplicateKey = suffix
            };
            _db.Students.Add(student);
            await _db.SaveChangesAsync();
            return student;
        }

        public async Task AddEnrollmentAsync(Student student, ClassData classRoom, Guid? sectionId)
        {
            _db.Enrollments.Add(new Enrollment
            {
                SchoolId = _schoolId,
                StudentId = student.Id,
                AcademicYearId = classRoom.AcademicYearId,
                ClassRoomId = classRoom.ClassRoomId,
                SectionId = sectionId,
                EnrollmentDate = new DateOnly(2025, 1, 1),
                Status = EnrollmentStatus.Enrolled
            });
            await _db.SaveChangesAsync();
        }

        public async Task AddStudentGuardianAsync(Student student, Guardian guardian)
        {
            _db.StudentGuardians.Add(new StudentGuardian
            {
                SchoolId = _schoolId,
                StudentId = student.Id,
                GuardianId = guardian.Id
            });
            await _db.SaveChangesAsync();
        }

        public RecordAccessService CreateRecordAccessService(SeededAccount account)
        {
            _tenant.UserId = account.IdentityUserId;
            return new RecordAccessService(
                _db,
                new UserPermissionService(_db, _identityDb, _tenant),
                _tenant);
        }
    }

    private sealed class FixedPermissionService(UserAccess access) : IUserPermissionService
    {
        public Task<UserAccess> GetAccessAsync(CancellationToken ct = default) =>
            Task.FromResult(access);
    }
}
