using Asms.Domain.Security;
using Asms.Domain.Schools;
using Asms.Infrastructure.Persistence;
using Asms.Infrastructure.Persistence.Seeding;
using Asms.Infrastructure.Tests.Persistence;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace Asms.Infrastructure.Tests.Seeding;

public class SchoolRoleSeederTests
{
    [Fact]
    public async Task Seeding_new_school_creates_roles_and_exact_permissions()
    {
        using var connection = CreateOpenConnection();
        var schoolId = Guid.NewGuid();
        await using var context = CreateContext(connection, schoolId);
        await AddSchoolAsync(context, schoolId);

        var result = await new SchoolRoleSeeder(context, new TestTenant { SchoolId = schoolId })
            .SeedAsync(schoolId);
        var roles = await context.AppRoles.IgnoreQueryFilters()
            .Where(role => role.SchoolId == schoolId)
            .ToListAsync();
        var permissions = await context.RolePermissions.IgnoreQueryFilters()
            .Where(permission => permission.SchoolId == schoolId)
            .ToListAsync();
        var expectedPermissionCount = DefaultRolePermissions.SchoolRoles
            .Sum(role => DefaultRolePermissions.ForRole(role).Count);

        Assert.Equal(10, result.RolesAdded);
        Assert.Equal(expectedPermissionCount, result.PermissionsAdded);
        Assert.Equal(10, roles.Count);
        Assert.Equal(expectedPermissionCount, permissions.Count);
        Assert.All(roles, role =>
        {
            Assert.True(role.IsSystemRole);
            Assert.Equal(schoolId, role.SchoolId);
            var actual = permissions
                .Where(permission => permission.RoleId == role.Id)
                .Select(permission => permission.Permission)
                .OrderBy(permission => permission);
            var expected = DefaultRolePermissions.ForRole(role.Name)
                .OrderBy(permission => permission);
            Assert.Equal(expected, actual);
        });
    }

    [Fact]
    public async Task Seeding_twice_adds_nothing_the_second_time()
    {
        using var connection = CreateOpenConnection();
        var schoolId = Guid.NewGuid();
        await using var context = CreateContext(connection, schoolId);
        await AddSchoolAsync(context, schoolId);
        var seeder = new SchoolRoleSeeder(context, new TestTenant { SchoolId = schoolId });

        await seeder.SeedAsync(schoolId);
        var secondResult = await seeder.SeedAsync(schoolId);

        Assert.Equal(new SeedResult(0, 0, 0, 0), secondResult);
    }

    [Fact]
    public async Task Seeding_one_school_does_not_create_roles_for_another_school()
    {
        using var connection = CreateOpenConnection();
        var schoolA = Guid.NewGuid();
        var schoolB = Guid.NewGuid();
        await using (var contextA = CreateContext(connection, schoolA))
        {
            contextA.Schools.AddRange(
                new School { Id = schoolA, Name = "School A" },
                new School { Id = schoolB, Name = "School B" });
            await contextA.SaveChangesAsync();
            await new SchoolRoleSeeder(contextA, new TestTenant { SchoolId = schoolA })
                .SeedAsync(schoolA);

            Assert.Equal(10, await contextA.AppRoles.CountAsync());
        }

        await using var contextB = CreateContext(connection, schoolB);
        Assert.Empty(await contextB.AppRoles.ToListAsync());

        await new SchoolRoleSeeder(contextB, new TestTenant { SchoolId = schoolB })
            .SeedAsync(schoolB);
        Assert.Equal(10, await contextB.AppRoles.CountAsync());
    }

    [Fact]
    public async Task Seeding_rejects_empty_school_and_tenant_mismatch()
    {
        using var connection = CreateOpenConnection();
        var tenantSchoolId = Guid.NewGuid();
        await using var context = CreateContext(connection, tenantSchoolId);
        var seeder = new SchoolRoleSeeder(
            context,
            new TestTenant { SchoolId = tenantSchoolId });

        await Assert.ThrowsAsync<ArgumentException>(() => seeder.SeedAsync(Guid.Empty));
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => seeder.SeedAsync(Guid.NewGuid()));
        Assert.Equal("Tenant does not match the school being seeded.", exception.Message);
    }

    [Fact]
    public async Task Seeding_restores_soft_deleted_role_and_permission()
    {
        using var connection = CreateOpenConnection();
        var schoolId = Guid.NewGuid();
        await using var context = CreateContext(connection, schoolId);
        await AddSchoolAsync(context, schoolId);
        var seeder = new SchoolRoleSeeder(context, new TestTenant { SchoolId = schoolId });
        await seeder.SeedAsync(schoolId);

        var owner = await context.AppRoles.SingleAsync(role => role.Name == SystemRoles.SchoolOwner);
        var permission = await context.RolePermissions.SingleAsync(row =>
            row.RoleId == owner.Id && row.Permission == Permissions.Students.View);
        owner.IsDeleted = true;
        permission.IsDeleted = true;
        await context.SaveChangesAsync();

        var result = await seeder.SeedAsync(schoolId);

        Assert.Equal(0, result.RolesAdded);
        Assert.Equal(1, result.RolesRestored);
        Assert.Equal(0, result.PermissionsAdded);
        Assert.Equal(1, result.PermissionsRestored);
        Assert.False(owner.IsDeleted);
        Assert.False(permission.IsDeleted);
        Assert.Equal(10, await context.AppRoles.IgnoreQueryFilters()
            .CountAsync(role => role.SchoolId == schoolId));
        Assert.Equal(
            DefaultRolePermissions.SchoolRoles.Sum(role => DefaultRolePermissions.ForRole(role).Count),
            await context.RolePermissions.IgnoreQueryFilters()
                .CountAsync(row => row.SchoolId == schoolId));
    }

    [Fact]
    public async Task Seeding_keeps_custom_permissions()
    {
        using var connection = CreateOpenConnection();
        var schoolId = Guid.NewGuid();
        await using var context = CreateContext(connection, schoolId);
        await AddSchoolAsync(context, schoolId);
        var seeder = new SchoolRoleSeeder(context, new TestTenant { SchoolId = schoolId });
        await seeder.SeedAsync(schoolId);
        var role = await context.AppRoles.SingleAsync(item => item.Name == SystemRoles.Teacher);
        context.RolePermissions.Add(new RolePermission
        {
            SchoolId = schoolId,
            RoleId = role.Id,
            Permission = "Custom.Access"
        });
        await context.SaveChangesAsync();

        var result = await seeder.SeedAsync(schoolId);

        Assert.Equal(new SeedResult(0, 0, 0, 0), result);
        Assert.True(await context.RolePermissions.IgnoreQueryFilters().AnyAsync(row =>
            row.SchoolId == schoolId &&
            row.RoleId == role.Id &&
            row.Permission == "Custom.Access"));
    }

    [Fact]
    public async Task Roles_created_have_the_school_id()
    {
        using var connection = CreateOpenConnection();
        var schoolId = Guid.NewGuid();
        await using var context = CreateContext(connection, schoolId);
        await AddSchoolAsync(context, schoolId);

        await new SchoolRoleSeeder(context, new TestTenant { SchoolId = schoolId })
            .SeedAsync(schoolId);

        Assert.All(
            await context.AppRoles.IgnoreQueryFilters()
                .Where(role => role.SchoolId == schoolId)
                .ToListAsync(),
            role => Assert.Equal(schoolId, role.SchoolId));
    }

    [Fact]
    public async Task Query_with_no_tenant_returns_rows_with_empty_SchoolId()
    {
        using var connection = CreateOpenConnection();
        await using var context = CreateContext(connection, null);

        // This is a known limit to handle in Phase 5.
        context.AppUsers.Add(new AppUser { FullName = "Platform user", SchoolId = Guid.Empty });
        await context.SaveChangesAsync();

        var user = Assert.Single(await context.AppUsers.ToListAsync());
        Assert.Equal(Guid.Empty, user.SchoolId);
    }

    private static async Task AddSchoolAsync(AsmsDbContext context, Guid schoolId)
    {
        context.Schools.Add(new School { Id = schoolId, Name = $"School {schoolId}" });
        await context.SaveChangesAsync();
    }

    private static SqliteConnection CreateOpenConnection()
    {
        var connection = new SqliteConnection("Data Source=:memory:");
        connection.Open();
        return connection;
    }

    private static AsmsDbContext CreateContext(SqliteConnection connection, Guid? schoolId)
    {
        var options = new DbContextOptionsBuilder<AsmsDbContext>()
            .UseSqlite(
                connection,
                sqlite => sqlite.MigrationsAssembly("Asms.Infrastructure.Migrations.Sqlite"))
            .Options;
        var context = new AsmsDbContext(options, new TestTenant { SchoolId = schoolId });
        context.Database.Migrate();
        return context;
    }
}
