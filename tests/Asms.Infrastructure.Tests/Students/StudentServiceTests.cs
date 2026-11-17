using Asms.Application.Students;
using Asms.Contracts.Students;
using Asms.Domain.Enums;
using Asms.Domain.Schools;
using Asms.Domain.Security;
using Asms.Domain.Students;
using Asms.Infrastructure.Identity;
using Asms.Infrastructure.Persistence;
using Asms.Infrastructure.Persistence.Seeding;
using Asms.Infrastructure.Security;
using Asms.Infrastructure.Students;
using Asms.Infrastructure.Tests.Persistence;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace Asms.Infrastructure.Tests.Students;

public sealed class StudentServiceTests
{
    [Fact]
    public async Task Create_generates_school_year_sequence_and_audit_excludes_contact_details()
    {
        await using var database = await StudentDatabase.CreateAsync();
        await database.AddUserAsync(SystemRoles.SchoolOwner);
        var service = database.CreateService();

        var first = await service.CreateAsync(NewRequest(
            firstName: "Amina",
            phoneNumber: "+93700123456",
            address: "Private address"));
        var second = await service.CreateAsync(NewRequest(
            firstName: "Zahra",
            phoneNumber: "+93700123457"));
        var otherYear = await service.CreateAsync(NewRequest(
            firstName: "Maryam",
            admissionDate: new DateOnly(2025, 9, 1)));

        Assert.True(first.Succeeded);
        Assert.Equal("DEMO1-2026-0001", first.Value!.StudentNumber);
        Assert.Equal("DEMO1-2026-0002", second.Value!.StudentNumber);
        Assert.Equal("DEMO1-2025-0001", otherYear.Value!.StudentNumber);

        var audit = await database.Db.AuditLogs.SingleAsync(
            log => log.Action == "Student.Created" && log.EntityId == first.Value.Id);
        Assert.Equal(first.Value.Id, audit.EntityId);
        Assert.Contains("Amina", audit.NewValuesJson);
        Assert.Contains(first.Value.Id.ToString(), audit.NewValuesJson);
        Assert.DoesNotContain("+93700123456", audit.NewValuesJson);
        Assert.DoesNotContain("Private address", audit.NewValuesJson);
    }

    [Fact]
    public async Task Duplicate_warning_returns_only_same_school_matches_and_confirmation_allows_creation()
    {
        await using var database = await StudentDatabase.CreateAsync();
        await database.AddUserAsync(SystemRoles.SchoolOwner);
        var service = database.CreateService();
        var original = await service.CreateAsync(NewRequest(firstName: "علي"));

        var duplicate = await service.CreateAsync(NewRequest(firstName: "علی"));

        Assert.True(original.Succeeded);
        Assert.False(duplicate.Succeeded);
        Assert.Equal("STUDENT_DUPLICATE_WARNING", duplicate.ErrorCode);
        Assert.Collection(duplicate.Duplicates, match =>
        {
            Assert.Equal(original.Value!.Id, match.Id);
            Assert.Equal(original.Value.StudentNumber, match.StudentNumber);
            Assert.Equal("علي", match.FirstName);
        });
        Assert.Equal(1, await database.Db.Students.CountAsync());

        var confirmed = await service.CreateAsync(NewRequest(
            firstName: "علی",
            confirmDuplicate: true));

        Assert.True(confirmed.Succeeded);
        Assert.Equal(2, await database.Db.Students.CountAsync());
    }

    [Fact]
    public async Task Guardian_links_support_new_and_existing_same_school_guardians()
    {
        await using var database = await StudentDatabase.CreateAsync();
        await database.AddUserAsync(SystemRoles.SchoolOwner);
        var existing = new Guardian
        {
            SchoolId = database.SchoolId,
            FullName = "Existing Guardian",
            PhoneNumber = "+93700999999"
        };
        database.Db.Guardians.Add(existing);
        await database.Db.SaveChangesAsync();

        var created = await database.CreateService().CreateAsync(NewRequest());
        var linked = await database.CreateService().CreateAsync(NewRequest(
            firstName: "Noor",
            guardians: [new GuardianLinkDto(
                existing.Id,
                null,
                null,
                null,
                nameof(GuardianRelationship.Mother),
                true,
                true)]));

        Assert.True(created.Succeeded);
        Assert.Single(created.Value!.Guardians);
        Assert.Equal("New Guardian", created.Value.Guardians[0].FullName);
        Assert.True(linked.Succeeded);
        Assert.Equal(existing.Id, Assert.Single(linked.Value!.Guardians).GuardianId);
        Assert.True(linked.Value.Guardians[0].IsEmergencyContact);
    }

    [Fact]
    public async Task Validation_and_guardian_tenant_checks_return_codes_without_creating_rows()
    {
        await using var database = await StudentDatabase.CreateAsync();
        await database.AddUserAsync(SystemRoles.SchoolOwner);
        var service = database.CreateService();

        var missingName = await service.CreateAsync(NewRequest(firstName: " "));
        var futureDate = await service.CreateAsync(NewRequest(
            firstName: "Future",
            dateOfBirth: DateOnly.FromDateTime(DateTime.UtcNow.AddDays(1))));
        var earlyDate = await service.CreateAsync(NewRequest(
            firstName: "Old",
            dateOfBirth: new DateOnly(1899, 12, 31)));
        var missingGuardian = await service.CreateAsync(NewRequest(guardians: []));
        var invalidGuardian = await service.CreateAsync(NewRequest(
            guardians: [new GuardianLinkDto(
                Guid.NewGuid(),
                null,
                null,
                null,
                nameof(GuardianRelationship.Father),
                true,
                false)]));

        Assert.Equal("STUDENT_REQUIRED_FIELD", missingName.ErrorCode);
        Assert.Equal("STUDENT_INVALID_DATES", futureDate.ErrorCode);
        Assert.Equal("STUDENT_INVALID_DATES", earlyDate.ErrorCode);
        Assert.Equal("STUDENT_GUARDIAN_REQUIRED", missingGuardian.ErrorCode);
        Assert.Equal("STUDENT_GUARDIAN_INVALID", invalidGuardian.ErrorCode);
        Assert.Empty(await database.Db.Students.ToListAsync());
        Assert.Empty(await database.Db.StudentGuardians.ToListAsync());
    }

    [Fact]
    public async Task Update_checks_version_keeps_student_number_and_school_and_audits_only_non_sensitive_changes()
    {
        await using var database = await StudentDatabase.CreateAsync();
        await database.AddUserAsync(SystemRoles.SchoolOwner);
        var service = database.CreateService();
        var created = await service.CreateAsync(NewRequest(phoneNumber: "+93700111111"));
        var studentId = created.Value!.Id;

        var updated = await service.UpdateAsync(
            studentId,
            UpdateFrom(created.Value, firstName: "Updated", phoneNumber: "+93700222222"));
        var stale = await service.UpdateAsync(
            studentId,
            UpdateFrom(created.Value, firstName: "Stale"));
        var stored = await database.Db.Students.SingleAsync(student => student.Id == studentId);
        var audit = await database.Db.AuditLogs.SingleAsync(log => log.Action == "Student.Updated");

        Assert.True(updated.Succeeded);
        Assert.Equal("DEMO1-2026-0001", updated.Value!.StudentNumber);
        Assert.Equal(database.SchoolId, stored.SchoolId);
        Assert.Equal("Updated", stored.FirstName);
        Assert.Equal(2, updated.Value.Version);
        Assert.Equal("STUDENT_VERSION_CONFLICT", stale.ErrorCode);
        Assert.Equal(2, await database.Db.AuditLogs.CountAsync());
        Assert.Contains("firstName", audit.OldValuesJson);
        Assert.DoesNotContain("phoneNumber", audit.OldValuesJson);
        Assert.DoesNotContain("+93700111111", audit.OldValuesJson);
        Assert.DoesNotContain("+93700222222", audit.NewValuesJson);
    }

    [Fact]
    public async Task Update_duplicate_warning_does_not_save_unconfirmed_changes()
    {
        await using var database = await StudentDatabase.CreateAsync();
        await database.AddUserAsync(SystemRoles.SchoolOwner);
        var service = database.CreateService();
        await service.CreateAsync(NewRequest(firstName: "Same"));
        var student = (await service.CreateAsync(NewRequest(firstName: "Other"))).Value!;

        var warning = await service.UpdateAsync(
            student.Id,
            UpdateFrom(student, firstName: "Same"));
        var confirmed = await service.UpdateAsync(
            student.Id,
            UpdateFrom(student, firstName: "Same", confirmDuplicate: true));

        Assert.Equal("STUDENT_DUPLICATE_WARNING", warning.ErrorCode);
        Assert.True(confirmed.Succeeded);
        Assert.Equal("Same", confirmed.Value!.FirstName);
    }

    [Fact]
    public async Task Accountant_can_view_but_cannot_create_or_edit_and_parent_cannot_write()
    {
        await using var database = await StudentDatabase.CreateAsync();
        var owner = await database.AddUserAsync(SystemRoles.SchoolOwner);
        var student = await database.CreateService().CreateAsync(NewRequest());
        var accountant = await database.AddUserAsync(SystemRoles.Accountant);
        var accountantService = database.CreateService(userId: accountant.IdentityUserId);
        var read = await accountantService.GetAsync(student.Value!.Id);
        var create = await accountantService.CreateAsync(NewRequest(firstName: "Blocked"));
        var edit = await accountantService.UpdateAsync(
            student.Value.Id,
            UpdateFrom(student.Value, firstName: "Blocked"));
        var parent = await database.AddUserAsync(SystemRoles.Parent);
        var parentCreate = await database.CreateService(userId: parent.IdentityUserId)
            .CreateAsync(NewRequest(firstName: "Blocked parent"));

        Assert.True(read.Succeeded);
        Assert.Equal("FORBIDDEN", create.ErrorCode);
        Assert.Equal("FORBIDDEN", edit.ErrorCode);
        Assert.Equal("FORBIDDEN", parentCreate.ErrorCode);
        Assert.Single(await database.Db.Students.ToListAsync());
        Assert.NotEqual(Guid.Empty, owner.IdentityUserId);
    }

    [Fact]
    public async Task Receptionist_can_create_and_teacher_scope_limits_search_and_get()
    {
        await using var database = await StudentDatabase.CreateAsync();
        var receptionist = await database.AddUserAsync(SystemRoles.Receptionist);
        var receptionistService = database.CreateService(userId: receptionist.IdentityUserId);
        var created = await receptionistService.CreateAsync(NewRequest());
        Assert.True(created.Succeeded);

        var teacher = await database.AddUserAsync(SystemRoles.Teacher);
        var teacherService = database.CreateService(userId: teacher.IdentityUserId);
        var page = await teacherService.SearchAsync(new StudentSearchQuery());
        var get = await teacherService.GetAsync(created.Value!.Id);

        Assert.Empty(page.Value!.Items);
        Assert.Equal(0, page.Value.TotalCount);
        Assert.Equal("FORBIDDEN", get.ErrorCode);
    }

    [Fact]
    public async Task Search_filters_text_and_status_and_returns_stable_pages_without_deleted_records()
    {
        await using var database = await StudentDatabase.CreateAsync();
        await database.AddUserAsync(SystemRoles.SchoolOwner);
        var service = database.CreateService();
        var first = await service.CreateAsync(NewRequest(
            firstName: "Alpha",
            lastName: "A",
            status: nameof(StudentStatus.Active)));
        await service.CreateAsync(NewRequest(
            firstName: "Beta",
            lastName: "B",
            status: nameof(StudentStatus.Withdrawn)));
        await service.CreateAsync(NewRequest(
            firstName: "Alia",
            lastName: "C",
            status: nameof(StudentStatus.Active)));
        var deleted = await database.Db.Students.SingleAsync(item => item.Id == first.Value!.Id);
        deleted.IsDeleted = true;
        await database.Db.SaveChangesAsync();

        var textPage = await service.SearchAsync(new StudentSearchQuery("Al", Page: 1, PageSize: 1));
        var withdrawn = await service.SearchAsync(new StudentSearchQuery(
            Status: nameof(StudentStatus.Withdrawn)));

        Assert.Single(textPage.Value!.Items);
        Assert.Equal(1, textPage.Value.TotalCount);
        Assert.Equal("Alia", textPage.Value.Items[0].FirstName);
        Assert.Single(withdrawn.Value!.Items);
        Assert.Equal(nameof(StudentStatus.Withdrawn), withdrawn.Value.Items[0].Status);
    }

    [Fact]
    public async Task Number_collision_retries_with_the_next_generated_number()
    {
        await using var database = await StudentDatabase.CreateAsync();
        await database.AddUserAsync(SystemRoles.SchoolOwner);
        database.Db.Students.Add(NewStoredStudent(database.SchoolId, "DEMO1-2026-0001"));
        await database.Db.SaveChangesAsync();
        var generator = new SequenceNumberGenerator(
            "DEMO1-2026-0001",
            "DEMO1-2026-0002");

        var result = await database.CreateService(generator).CreateAsync(NewRequest());

        Assert.True(result.Succeeded);
        Assert.Equal("DEMO1-2026-0002", result.Value!.StudentNumber);
        Assert.Equal(2, generator.Calls);
        Assert.Single(await database.Db.AuditLogs.Where(log => log.Action == "Student.Created").ToListAsync());
    }

    [Fact]
    public async Task Two_schools_use_independent_number_sequences_and_duplicate_scopes()
    {
        await using var database = await StudentDatabase.CreateAsync();
        await database.AddUserAsync(SystemRoles.SchoolOwner);
        var first = await database.CreateService().CreateAsync(NewRequest(firstName: "Shared"));
        var second = await database.AddSchoolAndOwnerAsync("Second school", "DEMO2");
        var secondStudent = await second.Service.CreateAsync(NewRequest(firstName: "Shared"));

        Assert.True(first.Succeeded);
        Assert.True(secondStudent.Succeeded);
        Assert.Equal("DEMO1-2026-0001", first.Value!.StudentNumber);
        Assert.Equal("DEMO2-2026-0001", secondStudent.Value!.StudentNumber);
    }

    [Fact]
    public async Task Concurrent_creates_use_distinct_student_numbers()
    {
        await using var database = await StudentDatabase.CreateAsync();
        var user = await database.AddUserAsync(SystemRoles.SchoolOwner);
        var first = database.CreateIndependentService(user.IdentityUserId);
        var second = database.CreateIndependentService(user.IdentityUserId);

        var results = await Task.WhenAll(
            first.Service.CreateAsync(NewRequest(firstName: "Concurrent A")),
            second.Service.CreateAsync(NewRequest(firstName: "Concurrent B")));

        Assert.All(results, result => Assert.True(result.Succeeded, result.ErrorCode));
        Assert.Equal(
            ["DEMO1-2026-0001", "DEMO1-2026-0002"],
            results.Select(result => result.Value!.StudentNumber).OrderBy(value => value).ToArray());
        await first.Lifetime.DisposeAsync();
        await second.Lifetime.DisposeAsync();
    }

    private static CreateStudentRequest NewRequest(
        string firstName = "First",
        string lastName = "Last",
        string fatherName = "Father",
        DateOnly? dateOfBirth = null,
        DateOnly? admissionDate = null,
        string? status = null,
        string? phoneNumber = "+93700123456",
        string? address = "Private address",
        IReadOnlyList<GuardianLinkDto>? guardians = null,
        bool confirmDuplicate = false) =>
        new(
            firstName,
            lastName,
            fatherName,
            null,
            dateOfBirth ?? new DateOnly(2015, 2, 1),
            null,
            null,
            null,
            address,
            phoneNumber,
            null,
            admissionDate ?? new DateOnly(2026, 3, 1),
            status,
            guardians ?? [NewGuardian()],
            confirmDuplicate);

    private static GuardianLinkDto NewGuardian() =>
        new(
            null,
            "New Guardian",
            "+93700123456",
            null,
            nameof(GuardianRelationship.Father),
            true,
            false);

    private static UpdateStudentRequest UpdateFrom(
        StudentDto student,
        string? firstName = null,
        string? phoneNumber = null,
        bool confirmDuplicate = false) =>
        new(
            student.Version,
            firstName ?? student.FirstName,
            student.LastName,
            student.FatherName,
            student.GrandfatherName,
            student.DateOfBirth,
            student.Gender,
            student.Province,
            student.District,
            student.Address,
            phoneNumber ?? student.PhoneNumber,
            student.PreviousSchool,
            student.AdmissionDate,
            student.Status,
            null,
            confirmDuplicate);

    private static Student NewStoredStudent(Guid schoolId, string studentNumber)
    {
        var student = new Student
        {
            SchoolId = schoolId,
            StudentNumber = studentNumber,
            FirstName = $"Stored-{Guid.NewGuid():N}",
            LastName = "Stored",
            FatherName = "Stored father",
            DateOfBirth = new DateOnly(2015, 1, 1),
            AdmissionDate = new DateOnly(2026, 1, 1)
        };
        student.RefreshDuplicateKey();
        return student;
    }

    private sealed class SequenceNumberGenerator(params string[] numbers) : IStudentNumberGenerator
    {
        private int _calls;

        public int Calls => _calls;

        public Task<string> GenerateAsync(
            Guid schoolId,
            string schoolCode,
            int admissionYear,
            CancellationToken ct = default)
        {
            var index = Interlocked.Increment(ref _calls) - 1;
            return Task.FromResult(numbers[Math.Min(index, numbers.Length - 1)]);
        }
    }

    private sealed class StudentDatabase : IAsyncDisposable
    {
        private readonly SqliteConnection _connection;
        private readonly DbContextOptions<AsmsDbContext> _dbOptions;
        private readonly DbContextOptions<AsmsIdentityDbContext> _identityOptions;

        private StudentDatabase()
        {
            var dataSource = $"asms-student-{Guid.NewGuid():N}";
            var connectionString = new SqliteConnectionStringBuilder
            {
                DataSource = dataSource,
                Mode = SqliteOpenMode.Memory,
                Cache = SqliteCacheMode.Shared
            }.ToString();
            _connection = new SqliteConnection(connectionString);
            _connection.Open();
            Tenant = new TestTenant { SchoolId = Guid.NewGuid() };
            SchoolId = Tenant.SchoolId!.Value;
            _dbOptions = new DbContextOptionsBuilder<AsmsDbContext>()
                .UseSqlite(
                    connectionString,
                    sqlite => sqlite.MigrationsAssembly("Asms.Infrastructure.Migrations.Sqlite"))
                .Options;
            _identityOptions = new DbContextOptionsBuilder<AsmsIdentityDbContext>()
                .UseSqlite(
                    connectionString,
                    sqlite => sqlite.MigrationsAssembly("Asms.Infrastructure.Migrations.Sqlite"))
                .Options;
            Db = new AsmsDbContext(_dbOptions, Tenant);
            IdentityDb = new AsmsIdentityDbContext(_identityOptions);
        }

        public Guid SchoolId { get; }

        public TestTenant Tenant { get; }

        public AsmsDbContext Db { get; }

        private AsmsIdentityDbContext IdentityDb { get; }

        public static async Task<StudentDatabase> CreateAsync()
        {
            var database = new StudentDatabase();
            await database.Db.Database.MigrateAsync();
            await database.IdentityDb.Database.MigrateAsync();
            database.Db.Schools.Add(new School
            {
                Id = database.SchoolId,
                Name = "Student test school",
                Code = "DEMO1"
            });
            await database.Db.SaveChangesAsync();
            await new SchoolRoleSeeder(database.Db, database.Tenant).SeedAsync(database.SchoolId);
            return database;
        }

        public async Task<(Guid IdentityUserId, Guid AppUserId)> AddUserAsync(string roleName)
        {
            var identityUserId = Guid.NewGuid();
            var appUserId = Guid.NewGuid();
            var roleId = await Db.AppRoles
                .Where(role => role.Name == roleName)
                .Select(role => role.Id)
                .SingleAsync();
            Db.AppUsers.Add(new AppUser
            {
                Id = appUserId,
                SchoolId = SchoolId,
                FullName = "Student service account"
            });
            Db.UserRoles.Add(new UserRole
            {
                SchoolId = SchoolId,
                UserId = appUserId,
                RoleId = roleId
            });
            await Db.SaveChangesAsync();
            IdentityDb.Users.Add(new AsmsIdentityUser
            {
                Id = identityUserId,
                UserName = $"student-{identityUserId:N}",
                NormalizedUserName = $"STUDENT-{identityUserId:N}",
                SchoolId = SchoolId,
                AppUserId = appUserId,
                IsActive = true
            });
            await IdentityDb.SaveChangesAsync();
            Tenant.UserId = identityUserId;
            return (identityUserId, appUserId);
        }

        public StudentService CreateService(
            IStudentNumberGenerator? generator = null,
            Guid? userId = null)
        {
            if (userId.HasValue)
            {
                Tenant.UserId = userId;
            }

            var permissions = new UserPermissionService(Db, IdentityDb, Tenant);
            return new StudentService(
                Db,
                Tenant,
                permissions,
                new RecordAccessService(Db, permissions, Tenant),
                generator ?? new StudentNumberGenerator(Db));
        }

        public async Task<(StudentService Service, IAsyncDisposable Lifetime)> AddSchoolAndOwnerAsync(
            string name,
            string code)
        {
            var schoolId = Guid.NewGuid();
            var tenant = new TestTenant { SchoolId = schoolId };
            var domain = new AsmsDbContext(_dbOptions, tenant);
            var identity = new AsmsIdentityDbContext(_identityOptions);
            domain.Schools.Add(new School { Id = schoolId, Name = name, Code = code });
            await domain.SaveChangesAsync();
            await new SchoolRoleSeeder(domain, tenant).SeedAsync(schoolId);
            var identityUserId = Guid.NewGuid();
            var appUserId = Guid.NewGuid();
            var roleId = await domain.AppRoles
                .Where(role => role.Name == SystemRoles.SchoolOwner)
                .Select(role => role.Id)
                .SingleAsync();
            domain.AppUsers.Add(new AppUser
            {
                Id = appUserId,
                SchoolId = schoolId,
                FullName = "Second school account"
            });
            domain.UserRoles.Add(new UserRole
            {
                SchoolId = schoolId,
                UserId = appUserId,
                RoleId = roleId
            });
            await domain.SaveChangesAsync();
            identity.Users.Add(new AsmsIdentityUser
            {
                Id = identityUserId,
                UserName = $"student-{identityUserId:N}",
                NormalizedUserName = $"STUDENT-{identityUserId:N}",
                SchoolId = schoolId,
                AppUserId = appUserId,
                IsActive = true
            });
            await identity.SaveChangesAsync();
            tenant.UserId = identityUserId;
            var permissions = new UserPermissionService(domain, identity, tenant);
            var service = new StudentService(
                domain,
                tenant,
                permissions,
                new RecordAccessService(domain, permissions, tenant),
                new StudentNumberGenerator(domain));
            return (service, new ContextLifetime(domain, identity));
        }

        public (StudentService Service, IAsyncDisposable Lifetime) CreateIndependentService(
            Guid userId)
        {
            var tenant = new TestTenant { SchoolId = SchoolId, UserId = userId };
            var domain = new AsmsDbContext(_dbOptions, tenant);
            var identity = new AsmsIdentityDbContext(_identityOptions);
            var permissions = new UserPermissionService(domain, identity, tenant);
            var service = new StudentService(
                domain,
                tenant,
                permissions,
                new RecordAccessService(domain, permissions, tenant),
                new StudentNumberGenerator(domain));
            return (service, new ContextLifetime(domain, identity));
        }

        public async ValueTask DisposeAsync()
        {
            await Db.DisposeAsync();
            await IdentityDb.DisposeAsync();
            await _connection.DisposeAsync();
        }

        private sealed class ContextLifetime(
            AsmsDbContext domain,
            AsmsIdentityDbContext identity) : IAsyncDisposable
        {
            public async ValueTask DisposeAsync()
            {
                await domain.DisposeAsync();
                await identity.DisposeAsync();
            }
        }
    }
}
