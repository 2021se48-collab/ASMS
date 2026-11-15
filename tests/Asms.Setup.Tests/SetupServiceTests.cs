using System.Security.Cryptography;
using Asms.Application.Abstractions;
using Asms.Application.Security;
using Asms.Domain.Security;
using Asms.Infrastructure.Identity;
using Asms.Infrastructure.Persistence;
using Asms.Infrastructure.Security;
using Asms.Setup;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Asms.Setup.Tests;

public sealed class SetupServiceTests
{
    [Fact]
    public async Task Successful_setup_creates_school_owner_and_permissions()
    {
        await using var database = SqliteTestDatabase.Create();
        var request = CreateRequest(database.ConnectionString);

        var result = await new SetupService().RunAsync(request);

        Assert.True(result.Succeeded, result.ErrorCode);
        Assert.NotNull(result.SchoolId);
        var tenant = new TestTenant { SchoolId = result.SchoolId };

        await using (var db = CreateApplicationContext(database.ConnectionString, tenant))
        {
            var school = await db.Schools.IgnoreQueryFilters()
                .SingleAsync(item => item.Id == result.SchoolId);
            Assert.Equal(request.SchoolName, school.Name);
            Assert.Equal(request.SchoolCode, school.Code);
            Assert.Equal(request.DefaultLanguage, school.DefaultLanguage);
            Assert.Equal("SolarHijri", school.CalendarType);
            Assert.True(school.IsActive);

            Assert.Equal(10, await db.AppRoles.IgnoreQueryFilters()
                .CountAsync(role => role.SchoolId == result.SchoolId));

            var owner = await db.AppUsers.IgnoreQueryFilters()
                .SingleAsync(user => user.SchoolId == result.SchoolId);
            Assert.Equal(request.OwnerFullName, owner.FullName);
            Assert.Equal(request.OwnerEmail, owner.Email);
            Assert.Equal(request.OwnerPhone, owner.PhoneNumber);
            Assert.Equal(request.DefaultLanguage, owner.PreferredLanguage);
            Assert.True(owner.IsActive);

            var ownerRole = await db.AppRoles.IgnoreQueryFilters()
                .SingleAsync(role =>
                    role.SchoolId == result.SchoolId &&
                    role.Name == SystemRoles.SchoolOwner);
            var link = await db.UserRoles.IgnoreQueryFilters()
                .SingleAsync(userRole =>
                    userRole.SchoolId == result.SchoolId &&
                    userRole.UserId == owner.Id);
            Assert.Equal(ownerRole.Id, link.RoleId);
            Assert.NotNull(owner.IdentityUserId);

            await using var services = CreateVerificationServices(database.ConnectionString, tenant);
            await using var scope = services.CreateAsyncScope();
            var userManager = scope.ServiceProvider.GetRequiredService<UserManager<AsmsIdentityUser>>();
            var identityUser = await userManager.FindByNameAsync(request.OwnerUserName);

            Assert.NotNull(identityUser);
            Assert.Equal(result.SchoolId, identityUser.SchoolId);
            Assert.Equal(owner.Id, identityUser.AppUserId);
            Assert.True(await userManager.CheckPasswordAsync(identityUser, request.OwnerPassword));
            Assert.False(
                identityUser.PasswordHash?.Contains(request.OwnerPassword, StringComparison.Ordinal) == true,
                "The stored password hash must not contain the supplied password.");

            tenant.UserId = identityUser.Id;
            var permissions = scope.ServiceProvider.GetRequiredService<IUserPermissionService>();
            var access = await permissions.GetAccessAsync();
            Assert.True(access.IsValid);
            Assert.Contains(SystemRoles.SchoolOwner, access.Roles);
            Assert.Equal(
                DefaultRolePermissions.ForRole(SystemRoles.SchoolOwner).OrderBy(item => item),
                access.Permissions.OrderBy(item => item));
        }
    }

    [Fact]
    public async Task Repeating_school_code_does_not_create_more_rows()
    {
        await using var database = SqliteTestDatabase.Create();
        var request = CreateRequest(database.ConnectionString);
        var service = new SetupService();

        var first = await service.RunAsync(request);
        Assert.True(first.Succeeded, first.ErrorCode);
        var before = await ReadCountsAsync(database.ConnectionString);

        var second = await service.RunAsync(request);
        var after = await ReadCountsAsync(database.ConnectionString);

        Assert.False(second.Succeeded);
        Assert.Equal("SETUP_SCHOOL_EXISTS", second.ErrorCode);
        Assert.Null(second.SchoolId);
        Assert.Equal(before, after);
    }

    [Fact]
    public async Task Weak_password_rolls_back_all_school_and_owner_rows()
    {
        await using var database = SqliteTestDatabase.Create();
        var request = CreateRequest(database.ConnectionString, CreateWeakPassword());

        var result = await new SetupService().RunAsync(request);
        var counts = await ReadCountsAsync(database.ConnectionString);

        Assert.False(result.Succeeded);
        Assert.Equal("SETUP_WEAK_PASSWORD", result.ErrorCode);
        Assert.Null(result.SchoolId);
        Assert.Equal(new SetupCounts(0, 0, 0, 0, 0, 0), counts);
    }

    [Theory]
    [InlineData("SchoolName")]
    [InlineData("SchoolCode")]
    [InlineData("DefaultLanguage")]
    [InlineData("Provider")]
    public async Task Invalid_input_is_rejected_before_database_changes(string field)
    {
        await using var database = SqliteTestDatabase.Create();
        var request = CreateRequest(database.ConnectionString);
        request = field switch
        {
            "SchoolName" => CopyRequest(request, schoolName: " "),
            "SchoolCode" => CopyRequest(request, schoolCode: "bad code"),
            "DefaultLanguage" => CopyRequest(request, defaultLanguage: "unknown"),
            "Provider" => CopyRequest(request, provider: "Unknown"),
            _ => throw new ArgumentOutOfRangeException(nameof(field))
        };

        var result = await new SetupService().RunAsync(request);

        Assert.False(result.Succeeded);
        Assert.Equal("SETUP_INVALID_INPUT", result.ErrorCode);
        Assert.Null(result.SchoolId);
        Assert.False(File.Exists(database.Path));
    }

    [Fact]
    public async Task Different_schools_remain_isolated_in_one_database()
    {
        await using var database = SqliteTestDatabase.Create();
        var service = new SetupService();
        var first = await service.RunAsync(CreateRequest(database.ConnectionString));
        var second = await service.RunAsync(CreateRequest(database.ConnectionString));

        Assert.True(first.Succeeded, first.ErrorCode);
        Assert.True(second.Succeeded, second.ErrorCode);
        Assert.NotEqual(first.SchoolId, second.SchoolId);

        await using var firstDb = CreateApplicationContext(
            database.ConnectionString,
            new TestTenant { SchoolId = first.SchoolId });
        await using var secondDb = CreateApplicationContext(
            database.ConnectionString,
            new TestTenant { SchoolId = second.SchoolId });

        Assert.Single(await firstDb.AppUsers.ToListAsync());
        Assert.Single(await secondDb.AppUsers.ToListAsync());
        Assert.Equal(2, await firstDb.Schools.IgnoreQueryFilters().CountAsync());
        Assert.Equal(10, await firstDb.AppRoles.IgnoreQueryFilters()
            .CountAsync(role => role.SchoolId == first.SchoolId));
        Assert.Equal(10, await secondDb.AppRoles.IgnoreQueryFilters()
            .CountAsync(role => role.SchoolId == second.SchoolId));
    }

    [Fact]
    public async Task Password_and_connection_string_are_absent_from_result_and_output()
    {
        await using var database = SqliteTestDatabase.Create();
        var request = CreateRequest(database.ConnectionString);
        var result = await new SetupService().RunAsync(request);
        using var output = new StringWriter();

        SetupConsoleOutput.WriteResult(output, request.SchoolCode, request.OwnerUserName, result);

        Assert.False(result.ToString().Contains(request.OwnerPassword, StringComparison.Ordinal));
        Assert.False(result.ToString().Contains(database.ConnectionString, StringComparison.Ordinal));
        Assert.False(output.ToString().Contains(request.OwnerPassword, StringComparison.Ordinal));
        Assert.False(output.ToString().Contains(database.ConnectionString, StringComparison.Ordinal));
        Assert.False(request.ToString().Contains(request.OwnerPassword, StringComparison.Ordinal));
        Assert.False(request.ToString().Contains(database.ConnectionString, StringComparison.Ordinal));
    }

    private static SetupRequest CreateRequest(string connectionString, string? password = null) =>
        new()
        {
            Provider = "Sqlite",
            ConnectionString = connectionString,
            SchoolName = $"School {Guid.NewGuid():N}",
            SchoolCode = $"S-{Guid.NewGuid():N}"[..10],
            Province = "Province",
            District = "District",
            DefaultLanguage = "en",
            OwnerFullName = $"Owner {Guid.NewGuid():N}",
            OwnerUserName = $"owner-{Guid.NewGuid():N}",
            OwnerEmail = $"owner-{Guid.NewGuid():N}@example.test",
            OwnerPhone = "0700000000",
            OwnerPassword = password ?? CreateStrongPassword()
        };

    private static SetupRequest CopyRequest(
        SetupRequest request,
        string? schoolName = null,
        string? schoolCode = null,
        string? defaultLanguage = null,
        string? provider = null) =>
        new()
        {
            Provider = provider ?? request.Provider,
            ConnectionString = request.ConnectionString,
            SchoolName = schoolName ?? request.SchoolName,
            SchoolCode = schoolCode ?? request.SchoolCode,
            Province = request.Province,
            District = request.District,
            DefaultLanguage = defaultLanguage ?? request.DefaultLanguage,
            OwnerFullName = request.OwnerFullName,
            OwnerUserName = request.OwnerUserName,
            OwnerEmail = request.OwnerEmail,
            OwnerPhone = request.OwnerPhone,
            OwnerPassword = request.OwnerPassword
        };

    private static string CreateStrongPassword()
    {
        string value;
        do
        {
            value = RandomNumberGenerator.GetString("abcdefghijklmnopqrstuvwxyz", 16);
        }
        while (value.Distinct().Count() < 4);

        return value + RandomNumberGenerator.GetInt32(0, 10);
    }

    private static string CreateWeakPassword() =>
        RandomNumberGenerator.GetString("abcdefghijklmnopqrstuvwxyz", 6);

    private static AsmsDbContext CreateApplicationContext(
        string connectionString,
        TestTenant tenant)
    {
        var options = new DbContextOptionsBuilder<AsmsDbContext>()
            .UseSqlite(
                connectionString,
                sqlite => sqlite.MigrationsAssembly("Asms.Infrastructure.Migrations.Sqlite"))
            .Options;
        return new AsmsDbContext(options, tenant);
    }

    private static ServiceProvider CreateVerificationServices(
        string connectionString,
        TestTenant tenant)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddAsmsIdentity(options =>
            options.UseSqlite(
                connectionString,
                sqlite => sqlite.MigrationsAssembly("Asms.Infrastructure.Migrations.Sqlite")));
        services.AddDbContext<AsmsDbContext>(options =>
            options.UseSqlite(
                connectionString,
                sqlite => sqlite.MigrationsAssembly("Asms.Infrastructure.Migrations.Sqlite")));
        services.AddSingleton<ICurrentTenant>(tenant);
        services.AddScoped<IUserPermissionService, UserPermissionService>();
        return services.BuildServiceProvider();
    }

    private static async Task<SetupCounts> ReadCountsAsync(string connectionString)
    {
        await using var db = CreateApplicationContext(connectionString, new TestTenant());
        var schools = await db.Schools.IgnoreQueryFilters().CountAsync();
        var roles = await db.AppRoles.IgnoreQueryFilters().CountAsync();
        var users = await db.AppUsers.IgnoreQueryFilters().CountAsync();
        var links = await db.UserRoles.IgnoreQueryFilters().CountAsync();
        var permissions = await db.RolePermissions.IgnoreQueryFilters().CountAsync();

        await using var services = CreateVerificationServices(connectionString, new TestTenant());
        await using var scope = services.CreateAsyncScope();
        var identityDb = scope.ServiceProvider.GetRequiredService<AsmsIdentityDbContext>();
        var identityUsers = await identityDb.Users.CountAsync();
        return new SetupCounts(schools, roles, users, links, permissions, identityUsers);
    }

    private sealed class TestTenant : ICurrentTenant
    {
        public Guid? SchoolId { get; set; }
        public Guid? UserId { get; set; }
        public Guid? DeviceId => null;
    }

    private sealed record SetupCounts(
        int Schools,
        int Roles,
        int Users,
        int UserRoles,
        int Permissions,
        int IdentityUsers);
}
