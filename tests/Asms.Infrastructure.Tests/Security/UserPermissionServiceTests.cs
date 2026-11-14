using Asms.Application.Security;
using Asms.Domain.Security;
using Asms.Domain.Schools;
using Asms.Infrastructure.Identity;
using Asms.Infrastructure.Persistence;
using Asms.Infrastructure.Persistence.Seeding;
using Asms.Infrastructure.Security;
using Asms.Infrastructure.Tests.Persistence;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace Asms.Infrastructure.Tests.Security;

public class UserPermissionServiceTests
{
    [Fact]
    public async Task Teacher_and_Accountant_receive_exactly_their_seeded_permissions()
    {
        await using var database = await PermissionDatabase.CreateAsync();
        var teacher = await database.AddAccountAsync(SystemRoles.Teacher);
        var accountant = await database.AddAccountAsync(SystemRoles.Accountant);

        var teacherAccess = await database.GetAccessAsync(teacher.IdentityUserId);
        var accountantAccess = await database.GetAccessAsync(accountant.IdentityUserId);

        Assert.Equal(
            DefaultRolePermissions.ForRole(SystemRoles.Teacher).OrderBy(value => value),
            teacherAccess.Permissions.OrderBy(value => value));
        Assert.Equal(
            DefaultRolePermissions.ForRole(SystemRoles.Accountant).OrderBy(value => value),
            accountantAccess.Permissions.OrderBy(value => value));
        Assert.DoesNotContain(accountantAccess.Permissions, value =>
            value.StartsWith("Attendance.", StringComparison.Ordinal) ||
            value.StartsWith("Exams.", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Parent_does_not_receive_Students_View()
    {
        await using var database = await PermissionDatabase.CreateAsync();
        var parent = await database.AddAccountAsync(SystemRoles.Parent);

        var access = await database.GetAccessAsync(parent.IdentityUserId);

        Assert.True(access.IsValid);
        Assert.False(access.Has(Permissions.Students.View));
    }

    [Fact]
    public async Task Multiple_roles_return_the_union_of_permissions()
    {
        await using var database = await PermissionDatabase.CreateAsync();
        var account = await database.AddAccountAsync(
            SystemRoles.Teacher,
            additionalRoleNames: [SystemRoles.Accountant]);

        var access = await database.GetAccessAsync(account.IdentityUserId);
        var expected = DefaultRolePermissions.ForRole(SystemRoles.Teacher)
            .Concat(DefaultRolePermissions.ForRole(SystemRoles.Accountant))
            .ToHashSet(StringComparer.Ordinal);

        Assert.True(access.IsValid);
        Assert.Equal(expected, access.Permissions);
    }

    [Fact]
    public async Task Invalid_tenant_and_account_states_are_denied()
    {
        await using var database = await PermissionDatabase.CreateAsync();
        var ordinary = await database.AddAccountAsync(SystemRoles.Teacher);

        database.Tenant.SchoolId = null;
        database.Tenant.UserId = ordinary.IdentityUserId;
        Assert.False((await database.GetAccessAsync()).IsValid);

        database.Tenant.SchoolId = database.SchoolId;
        database.Tenant.UserId = null;
        Assert.False((await database.GetAccessAsync()).IsValid);

        Assert.False((await database.GetAccessAsync(Guid.NewGuid())).IsValid);

        var inactiveIdentity = await database.AddAccountAsync(
            SystemRoles.Teacher,
            isIdentityActive: false);
        Assert.False((await database.GetAccessAsync(inactiveIdentity.IdentityUserId)).IsValid);

        var mismatchedIdentitySchool = await database.AddAccountAsync(
            SystemRoles.Teacher,
            identitySchoolId: Guid.NewGuid());
        Assert.False((await database.GetAccessAsync(mismatchedIdentitySchool.IdentityUserId)).IsValid);

        var platformAccount = await database.AddAccountAsync(
            SystemRoles.Teacher,
            identitySchoolId: Guid.Empty);
        database.Tenant.SchoolId = Guid.Empty;
        Assert.False((await database.GetAccessAsync(platformAccount.IdentityUserId)).IsValid);
        database.Tenant.SchoolId = database.SchoolId;

        var noProfile = await database.AddAccountAsync(
            SystemRoles.Teacher,
            includeAppUser: false);
        Assert.False((await database.GetAccessAsync(noProfile.IdentityUserId)).IsValid);

        var inactiveProfile = await database.AddAccountAsync(
            SystemRoles.Teacher,
            isAppUserActive: false);
        Assert.False((await database.GetAccessAsync(inactiveProfile.IdentityUserId)).IsValid);

        var deletedProfile = await database.AddAccountAsync(
            SystemRoles.Teacher,
            isAppUserDeleted: true);
        Assert.False((await database.GetAccessAsync(deletedProfile.IdentityUserId)).IsValid);

        var noRoles = await database.AddAccountAsync();
        Assert.False((await database.GetAccessAsync(noRoles.IdentityUserId)).IsValid);
    }

    [Fact]
    public async Task Inactive_or_deleted_school_is_denied()
    {
        await using var database = await PermissionDatabase.CreateAsync();
        var account = await database.AddAccountAsync(SystemRoles.Teacher);

        var school = await database.Db.Schools.IgnoreQueryFilters()
            .SingleAsync(item => item.Id == database.SchoolId);
        school.IsActive = false;
        await database.Db.SaveChangesAsync();
        Assert.False((await database.GetAccessAsync(account.IdentityUserId)).IsValid);

        school.IsActive = true;
        school.IsDeleted = true;
        await database.Db.SaveChangesAsync();
        Assert.False((await database.GetAccessAsync(account.IdentityUserId)).IsValid);
    }

    [Fact]
    public async Task Soft_deleted_memberships_roles_and_permissions_are_ignored()
    {
        await using var database = await PermissionDatabase.CreateAsync();
        var deletedMembership = await database.AddAccountAsync(SystemRoles.Teacher);
        var membership = await database.Db.UserRoles.IgnoreQueryFilters()
            .SingleAsync(item => item.UserId == deletedMembership.AppUserId);
        membership.IsDeleted = true;
        await database.Db.SaveChangesAsync();
        Assert.False((await database.GetAccessAsync(deletedMembership.IdentityUserId)).IsValid);

        var deletedRole = await database.AddAccountAsync(SystemRoles.Teacher);
        var role = await database.Db.AppRoles.IgnoreQueryFilters()
            .SingleAsync(item => item.Id == deletedRole.RoleIds[0]);
        role.IsDeleted = true;
        await database.Db.SaveChangesAsync();
        Assert.False((await database.GetAccessAsync(deletedRole.IdentityUserId)).IsValid);

        role.IsDeleted = false;
        await database.Db.SaveChangesAsync();
        var deletedPermission = await database.AddAccountAsync(SystemRoles.Teacher);
        var teacherRoleId = deletedPermission.RoleIds[0];
        var permission = await database.Db.RolePermissions.IgnoreQueryFilters()
            .SingleAsync(item =>
                item.RoleId == teacherRoleId &&
                item.Permission == Permissions.Students.View);
        permission.IsDeleted = true;
        await database.Db.SaveChangesAsync();

        var access = await database.GetAccessAsync(deletedPermission.IdentityUserId);
        Assert.True(access.IsValid);
        Assert.DoesNotContain(Permissions.Students.View, access.Permissions);
    }

    [Fact]
    public async Task Other_school_role_with_same_name_does_not_add_its_permissions()
    {
        await using var database = await PermissionDatabase.CreateAsync();
        var otherSchoolId = await database.AddSchoolAsync();
        var account = await database.AddAccountAsync(SystemRoles.Teacher);
        var otherTeacher = await database.GetRoleAsync(otherSchoolId, SystemRoles.Teacher);
        await database.AddRolePermissionAsync(
            otherSchoolId,
            otherTeacher.Id,
            Permissions.Audit.View);
        database.Db.UserRoles.Add(new UserRole
        {
            SchoolId = database.SchoolId,
            UserId = account.AppUserId,
            RoleId = otherTeacher.Id
        });
        await database.Db.SaveChangesAsync();

        var access = await database.GetAccessAsync(account.IdentityUserId);

        Assert.Equal([SystemRoles.Teacher], access.Roles);
        Assert.DoesNotContain(Permissions.Audit.View, access.Permissions);
    }

    [Fact]
    public async Task New_service_instance_observes_removed_permission_while_existing_instance_is_cached()
    {
        await using var database = await PermissionDatabase.CreateAsync();
        var account = await database.AddAccountAsync(SystemRoles.Teacher);
        var service = database.CreatePermissionService(account.IdentityUserId);

        var firstAccess = await service.GetAccessAsync();
        var permission = await database.Db.RolePermissions.IgnoreQueryFilters()
            .SingleAsync(item =>
                item.RoleId == account.RoleIds[0] &&
                item.Permission == Permissions.Students.View);
        permission.IsDeleted = true;
        await database.Db.SaveChangesAsync();
        var cachedAccess = await service.GetAccessAsync();
        var freshAccess = await database.GetAccessAsync(account.IdentityUserId);

        Assert.True(firstAccess.Has(Permissions.Students.View));
        Assert.True(cachedAccess.Has(Permissions.Students.View));
        Assert.False(freshAccess.Has(Permissions.Students.View));
    }

    [Fact]
    public async Task Unknown_permission_strings_are_ignored()
    {
        await using var database = await PermissionDatabase.CreateAsync();
        var account = await database.AddAccountAsync(SystemRoles.Teacher);
        database.Db.RolePermissions.Add(new RolePermission
        {
            SchoolId = database.SchoolId,
            RoleId = account.RoleIds[0],
            Permission = "Unknown.Permission"
        });
        await database.Db.SaveChangesAsync();

        var access = await database.GetAccessAsync(account.IdentityUserId);

        Assert.True(access.IsValid);
        Assert.DoesNotContain("Unknown.Permission", access.Permissions);
    }

    [Fact]
    public async Task Database_failure_returns_denied()
    {
        await using var database = await PermissionDatabase.CreateAsync();
        var disposedIdentityContext = database.CreateIdentityContext();
        await disposedIdentityContext.DisposeAsync();
        var service = new UserPermissionService(
            database.Db,
            disposedIdentityContext,
            database.Tenant);

        var access = await service.GetAccessAsync();

        Assert.Same(UserAccess.Denied, access);
    }

    private sealed record SeededAccount(
        Guid IdentityUserId,
        Guid AppUserId,
        Guid[] RoleIds);

    private sealed class PermissionDatabase : IAsyncDisposable
    {
        private readonly SqliteConnection _connection;
        private readonly DbContextOptions<AsmsDbContext> _dbOptions;
        private readonly DbContextOptions<AsmsIdentityDbContext> _identityOptions;

        private PermissionDatabase()
        {
            _connection = new SqliteConnection("Data Source=:memory:");
            _connection.Open();
            Tenant = new TestTenant { SchoolId = Guid.NewGuid() };
            SchoolId = Tenant.SchoolId!.Value;
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

        public static async Task<PermissionDatabase> CreateAsync()
        {
            var database = new PermissionDatabase();
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

        public async Task<SeededAccount> AddAccountAsync(
            string? roleName = null,
            bool isIdentityActive = true,
            Guid? identitySchoolId = null,
            bool includeAppUser = true,
            bool isAppUserActive = true,
            bool isAppUserDeleted = false,
            params string[] additionalRoleNames)
        {
            var roleNames = (roleName is null ? Array.Empty<string>() : [roleName])
                .Concat(additionalRoleNames)
                .Distinct(StringComparer.Ordinal)
                .ToArray();
            var appUserId = Guid.NewGuid();
            if (includeAppUser)
            {
                Db.AppUsers.Add(new AppUser
                {
                    Id = appUserId,
                    SchoolId = SchoolId,
                    FullName = "Test account",
                    IsActive = isAppUserActive,
                    IsDeleted = isAppUserDeleted
                });
            }

            var roleIds = new List<Guid>();
            foreach (var accountRoleName in roleNames)
            {
                if (!includeAppUser)
                {
                    break;
                }

                var role = await Db.AppRoles.SingleAsync(item => item.Name == accountRoleName);
                Db.UserRoles.Add(new UserRole
                {
                    SchoolId = SchoolId,
                    UserId = appUserId,
                    RoleId = role.Id
                });
                roleIds.Add(role.Id);
            }

            await Db.SaveChangesAsync();

            var identityUserId = Guid.NewGuid();
            IdentityDb.Users.Add(new AsmsIdentityUser
            {
                Id = identityUserId,
                UserName = $"user-{identityUserId:N}",
                NormalizedUserName = $"USER-{identityUserId:N}",
                SchoolId = identitySchoolId ?? SchoolId,
                AppUserId = includeAppUser ? appUserId : null,
                IsActive = isIdentityActive
            });
            await IdentityDb.SaveChangesAsync();

            return new SeededAccount(identityUserId, appUserId, roleIds.ToArray());
        }

        public async Task<Guid> AddSchoolAsync()
        {
            var schoolId = Guid.NewGuid();
            await using var db = CreateDomainContext(schoolId);
            db.Schools.Add(new School { Id = schoolId, Name = $"School {schoolId:N}" });
            await db.SaveChangesAsync();
            await new SchoolRoleSeeder(db, new TestTenant { SchoolId = schoolId })
                .SeedAsync(schoolId);
            return schoolId;
        }

        public async Task<AppRole> GetRoleAsync(Guid schoolId, string name)
        {
            await using var db = CreateDomainContext(schoolId);
            return await db.AppRoles.IgnoreQueryFilters()
                .SingleAsync(role => role.SchoolId == schoolId && role.Name == name);
        }

        public async Task AddRolePermissionAsync(Guid schoolId, Guid roleId, string permission)
        {
            await using var db = CreateDomainContext(schoolId);
            db.RolePermissions.Add(new RolePermission
            {
                SchoolId = schoolId,
                RoleId = roleId,
                Permission = permission
            });
            await db.SaveChangesAsync();
        }

        public async Task<UserAccess> GetAccessAsync(Guid? userId = null)
        {
            if (userId.HasValue)
            {
                Tenant.UserId = userId;
            }

            return await CreatePermissionService().GetAccessAsync();
        }

        public UserPermissionService CreatePermissionService(Guid? userId = null)
        {
            if (userId.HasValue)
            {
                Tenant.UserId = userId;
            }

            return new UserPermissionService(Db, IdentityDb, Tenant);
        }

        public AsmsIdentityDbContext CreateIdentityContext() =>
            new(_identityOptions);

        private AsmsDbContext CreateDomainContext(Guid? schoolId) =>
            new(_dbOptions, new TestTenant { SchoolId = schoolId });

        public async ValueTask DisposeAsync()
        {
            await Db.DisposeAsync();
            await IdentityDb.DisposeAsync();
            await _connection.DisposeAsync();
        }
    }
}
