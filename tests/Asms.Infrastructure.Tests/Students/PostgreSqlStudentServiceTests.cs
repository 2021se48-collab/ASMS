using Asms.Application.Students;
using Asms.Contracts.Students;
using Asms.Domain.Enums;
using Asms.Domain.Schools;
using Asms.Domain.Security;
using Asms.Infrastructure.Identity;
using Asms.Infrastructure.Persistence;
using Asms.Infrastructure.Persistence.Seeding;
using Asms.Infrastructure.Security;
using Asms.Infrastructure.Students;
using Asms.Infrastructure.Tests.Persistence;
using Asms.Infrastructure.Tests.PostgreSql;
using Microsoft.EntityFrameworkCore;

namespace Asms.Infrastructure.Tests.Students;

public sealed class PostgreSqlStudentServiceTests
{
    [PostgresFact]
    public async Task PostgreSql_create_duplicate_warning_and_search_work()
    {
        await using var fixture = await PostgreSqlStudentFixture.CreateAsync();
        var service = fixture.CreateService();

        var created = await service.CreateAsync(NewRequest(firstName: "Amina"));
        var duplicate = await service.CreateAsync(NewRequest(firstName: "Amina"));
        var search = await service.SearchAsync(new StudentSearchQuery(Text: "Amina"));

        Assert.True(created.Succeeded, created.ErrorCode);
        Assert.Equal("DEMO-PG-2026-0001", created.Value!.StudentNumber);
        Assert.Equal("STUDENT_DUPLICATE_WARNING", duplicate.ErrorCode);
        Assert.Contains(duplicate.Duplicates, match => match.Id == created.Value.Id);
        Assert.True(search.Succeeded, search.ErrorCode);
        Assert.Equal(1, search.Value!.TotalCount);
        Assert.Equal(created.Value.Id, Assert.Single(search.Value.Items).Id);
    }

    [PostgresFact]
    public async Task PostgreSql_concurrent_creates_receive_distinct_student_numbers()
    {
        await using var fixture = await PostgreSqlStudentFixture.CreateAsync();

        var results = await Task.WhenAll(
            fixture.CreateAsync(NewRequest(firstName: "Concurrent A")),
            fixture.CreateAsync(NewRequest(firstName: "Concurrent B")));

        Assert.All(results, result => Assert.True(result.Succeeded, result.ErrorCode));
        Assert.Equal(
            ["DEMO-PG-2026-0001", "DEMO-PG-2026-0002"],
            results.Select(result => result.Value!.StudentNumber).OrderBy(value => value).ToArray());
    }

    private static CreateStudentRequest NewRequest(string firstName) =>
        new(
            firstName,
            "Student",
            "Father",
            null,
            new DateOnly(2015, 2, 1),
            null,
            null,
            null,
            "Private address",
            "+93700123456",
            null,
            new DateOnly(2026, 3, 1),
            null,
            [
                new GuardianLinkDto(
                    null,
                    "New Guardian",
                    "+93700123456",
                    null,
                    nameof(GuardianRelationship.Father),
                    true,
                    false)
            ],
            false);

    private sealed class PostgreSqlStudentFixture : IAsyncDisposable
    {
        private readonly PostgresTestDatabase _database;
        private readonly TestTenant _tenant;
        private readonly AsmsDbContext _domain;
        private readonly AsmsIdentityDbContext _identity;

        private PostgreSqlStudentFixture(
            PostgresTestDatabase database,
            TestTenant tenant,
            AsmsDbContext domain,
            AsmsIdentityDbContext identity)
        {
            _database = database;
            _tenant = tenant;
            _domain = domain;
            _identity = identity;
        }

        public static async Task<PostgreSqlStudentFixture> CreateAsync()
        {
            var database = await PostgresTestDatabase.CreateAsync();
            var tenant = new TestTenant
            {
                SchoolId = Guid.NewGuid(),
                UserId = Guid.NewGuid()
            };
            var domain = database.CreateContext(tenant);
            var identityOptions = new DbContextOptionsBuilder<AsmsIdentityDbContext>();
            database.ConfigureIdentityDbContext(identityOptions);
            var identity = new AsmsIdentityDbContext(identityOptions.Options);

            try
            {
                await domain.Database.MigrateAsync();
                await identity.Database.MigrateAsync();
                domain.Schools.Add(new School
                {
                    Id = tenant.SchoolId!.Value,
                    Name = "PostgreSQL student test school",
                    Code = "DEMO-PG"
                });
                await domain.SaveChangesAsync();
                await new SchoolRoleSeeder(domain, tenant).SeedAsync(tenant.SchoolId.Value);

                var appUserId = Guid.NewGuid();
                var roleId = await domain.AppRoles
                    .Where(role => role.Name == SystemRoles.SchoolOwner)
                    .Select(role => role.Id)
                    .SingleAsync();
                domain.AppUsers.Add(new AppUser
                {
                    Id = appUserId,
                    SchoolId = tenant.SchoolId.Value,
                    FullName = "PostgreSQL student test owner"
                });
                domain.UserRoles.Add(new UserRole
                {
                    SchoolId = tenant.SchoolId.Value,
                    UserId = appUserId,
                    RoleId = roleId
                });
                await domain.SaveChangesAsync();
                identity.Users.Add(new AsmsIdentityUser
                {
                    Id = tenant.UserId!.Value,
                    UserName = $"student-{tenant.UserId:N}",
                    NormalizedUserName = $"STUDENT-{tenant.UserId:N}",
                    SchoolId = tenant.SchoolId.Value,
                    AppUserId = appUserId,
                    IsActive = true
                });
                await identity.SaveChangesAsync();

                return new PostgreSqlStudentFixture(database, tenant, domain, identity);
            }
            catch
            {
                await domain.DisposeAsync();
                await identity.DisposeAsync();
                await database.DisposeAsync();
                throw;
            }
        }

        public async Task<StudentResult<StudentDto>> CreateAsync(CreateStudentRequest request)
        {
            var tenant = new TestTenant
            {
                SchoolId = _tenant.SchoolId,
                UserId = _tenant.UserId
            };
            await using var domain = _database.CreateContext(tenant);
            var identityOptions = new DbContextOptionsBuilder<AsmsIdentityDbContext>();
            _database.ConfigureIdentityDbContext(identityOptions);
            await using var identity = new AsmsIdentityDbContext(identityOptions.Options);

            return await CreateService(domain, identity, tenant).CreateAsync(request);
        }

        public StudentService CreateService(
            AsmsDbContext? domain = null,
            AsmsIdentityDbContext? identity = null,
            TestTenant? tenant = null)
        {
            domain ??= _domain;
            identity ??= _identity;
            tenant ??= _tenant;
            var permissions = new UserPermissionService(domain, identity, tenant);
            return new StudentService(
                domain,
                tenant,
                permissions,
                new RecordAccessService(domain, permissions, tenant),
                new StudentNumberGenerator(domain));
        }

        public async ValueTask DisposeAsync()
        {
            await _domain.DisposeAsync();
            await _identity.DisposeAsync();
            await _database.DisposeAsync();
        }
    }
}
