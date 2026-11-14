using System.Security.Cryptography;
using Asms.Application.Auth;
using Asms.Application.Abstractions;
using Asms.Domain.Security;
using Asms.Domain.Schools;
using Asms.Infrastructure.Identity;
using Asms.Infrastructure.Persistence;
using Asms.Infrastructure.Security;
using Asms.Infrastructure.Tests.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Asms.Infrastructure.Tests.Identity;

public class AuthServiceTests
{
    [Fact]
    public async Task Successful_login_returns_token_and_roles_and_sets_last_login()
    {
        using var database = new AuthTestDatabase();
        var account = await database.CreateAccountAsync();
        using var scope = database.Services.CreateScope();
        var authService = scope.ServiceProvider.GetRequiredService<IAuthService>();

        var result = await authService.LoginAsync(account.UserName, account.Password);

        Assert.True(result.Succeeded);
        Assert.NotNull(result.AccessToken);
        Assert.Equal(account.IdentityUser.Id, result.UserId);
        Assert.Equal(account.SchoolId, result.SchoolId);
        Assert.Equal([account.RoleName], result.Roles);

        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<AsmsIdentityUser>>();
        var storedUser = await userManager.FindByIdAsync(account.IdentityUser.Id.ToString());
        Assert.NotNull(storedUser?.LastLoginAtUtc);
    }

    [Fact]
    public async Task Login_failures_return_no_token_for_invalid_credentials_and_accounts()
    {
        using var database = new AuthTestDatabase();
        var active = await database.CreateAccountAsync();
        var inactiveUser = await database.CreateAccountAsync(isUserActive: false);
        var inactiveSchool = await database.CreateAccountAsync(isSchoolActive: false);
        var deletedSchool = await database.CreateAccountAsync(isSchoolDeleted: true);
        var userWithoutRoles = await database.CreateAccountAsync(includeRole: false);
        using var scope = database.Services.CreateScope();
        var authService = scope.ServiceProvider.GetRequiredService<IAuthService>();

        AssertFailed(await authService.LoginAsync(active.UserName, CreatePassword()));
        AssertFailed(await authService.LoginAsync(CreateUserName(), CreatePassword()));
        AssertFailed(await authService.LoginAsync(inactiveUser.UserName, inactiveUser.Password));
        AssertFailed(await authService.LoginAsync(inactiveSchool.UserName, inactiveSchool.Password));
        AssertFailed(await authService.LoginAsync(deletedSchool.UserName, deletedSchool.Password));
        AssertFailed(await authService.LoginAsync(userWithoutRoles.UserName, userWithoutRoles.Password));
    }

    [Fact]
    public async Task Five_wrong_passwords_lock_account_until_lockout_expires()
    {
        using var database = new AuthTestDatabase();
        var account = await database.CreateAccountAsync();
        using var scope = database.Services.CreateScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<AsmsIdentityUser>>();
        var user = await userManager.FindByNameAsync(account.UserName);
        Assert.NotNull(user);

        for (var attempt = 0; attempt < 5; attempt++)
        {
            AssertFailed(await database.AuthService(scope).LoginAsync(account.UserName, CreatePassword()));
        }

        Assert.True(await userManager.IsLockedOutAsync(user));
        AssertFailed(await database.AuthService(scope).LoginAsync(account.UserName, account.Password));

        Assert.True((await userManager.SetLockoutEndDateAsync(user, DateTimeOffset.UtcNow.AddSeconds(-1))).Succeeded);
        Assert.True((await database.AuthService(scope).LoginAsync(account.UserName, account.Password)).Succeeded);
    }

    [Fact]
    public async Task Successful_login_resets_failed_attempt_count()
    {
        using var database = new AuthTestDatabase();
        var account = await database.CreateAccountAsync();
        using var scope = database.Services.CreateScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<AsmsIdentityUser>>();
        var user = await userManager.FindByNameAsync(account.UserName);
        Assert.NotNull(user);
        Assert.True((await userManager.AccessFailedAsync(user)).Succeeded);
        Assert.True((await userManager.AccessFailedAsync(user)).Succeeded);

        Assert.True((await database.AuthService(scope).LoginAsync(account.UserName, account.Password)).Succeeded);

        Assert.Equal(0, await userManager.GetAccessFailedCountAsync(user));
    }

    [Fact]
    public async Task Roles_from_other_schools_and_deleted_roles_are_not_returned()
    {
        using var database = new AuthTestDatabase();
        var schoolId = Guid.NewGuid();
        var otherSchoolId = Guid.NewGuid();
        var account = await database.CreateAccountAsync(
            schoolId: schoolId,
            extraRoles:
            [
                new RoleSetup("CrossSchool", otherSchoolId),
                new RoleSetup("DeletedRole", schoolId, IsDeleted: true)
            ]);
        using var scope = database.Services.CreateScope();

        var result = await database.AuthService(scope).LoginAsync(account.UserName, account.Password);

        Assert.True(result.Succeeded);
        Assert.Equal([account.RoleName], result.Roles);
        Assert.DoesNotContain("CrossSchool", result.Roles);
        Assert.DoesNotContain("DeletedRole", result.Roles);
    }

    [Fact]
    public async Task Users_in_different_schools_receive_only_their_own_roles()
    {
        using var database = new AuthTestDatabase();
        const string userNamePrefix = "teacher-";
        var schoolA = await database.CreateAccountAsync(userName: $"{userNamePrefix}{Guid.NewGuid():N}");
        var schoolB = await database.CreateAccountAsync(userName: $"{userNamePrefix}{Guid.NewGuid():N}");
        using var scope = database.Services.CreateScope();
        var authService = database.AuthService(scope);

        var loginA = await authService.LoginAsync(schoolA.UserName, schoolA.Password);
        var loginB = await authService.LoginAsync(schoolB.UserName, schoolB.Password);

        Assert.Equal(schoolA.SchoolId, loginA.SchoolId);
        Assert.Equal([schoolA.RoleName], loginA.Roles);
        Assert.DoesNotContain(schoolB.RoleName, loginA.Roles);
        Assert.Equal(schoolB.SchoolId, loginB.SchoolId);
        Assert.Equal([schoolB.RoleName], loginB.Roles);
        Assert.DoesNotContain(schoolA.RoleName, loginB.Roles);
    }

    [Fact]
    public async Task Unknown_user_uses_dummy_hash_path_without_throwing()
    {
        using var database = new AuthTestDatabase();
        using var scope = database.Services.CreateScope();

        var result = await database.AuthService(scope).LoginAsync(CreateUserName(), CreatePassword());

        AssertFailed(result);
    }

    private static void AssertFailed(AuthResult result)
    {
        Assert.False(result.Succeeded);
        Assert.Null(result.AccessToken);
        Assert.Null(result.ExpiresAtUtc);
        Assert.Null(result.UserId);
        Assert.Null(result.SchoolId);
        Assert.Empty(result.Roles);
    }

    private static string CreatePassword() => $"Aa1{Guid.NewGuid():N}";

    private static string CreateUserName() => $"user-{Guid.NewGuid():N}";

    private sealed record RoleSetup(string Name, Guid SchoolId, bool IsDeleted = false);

    private sealed record Account(
        Guid SchoolId,
        string UserName,
        string Password,
        string RoleName,
        AsmsIdentityUser IdentityUser);

    private sealed class AuthTestDatabase : IDisposable
    {
        private readonly SqliteConnection _connection;

        public AuthTestDatabase()
        {
            _connection = new SqliteConnection(new SqliteConnectionStringBuilder
            {
                DataSource = ":memory:"
            }.ToString());
            _connection.Open();

            var services = new ServiceCollection();
            services.AddLogging();
            services.AddSingleton<ICurrentTenant>(new TestTenant());
            services.AddDbContext<AsmsDbContext>(options =>
                options.UseSqlite(
                    _connection,
                    sqlite => sqlite.MigrationsAssembly("Asms.Infrastructure.Migrations.Sqlite")));
            services.AddAsmsIdentity(options =>
                options.UseSqlite(
                    _connection,
                    sqlite => sqlite.MigrationsAssembly("Asms.Infrastructure.Migrations.Sqlite")));
            services.Configure<JwtOptions>(options =>
                options.SigningKey = Convert.ToHexString(RandomNumberGenerator.GetBytes(32)));
            services.AddSingleton<ITokenService, JwtTokenService>();
            services.AddSingleton(TimeProvider.System);
            services.AddScoped<IAuthService, AuthService>();
            Services = services.BuildServiceProvider();

            using var scope = Services.CreateScope();
            scope.ServiceProvider.GetRequiredService<AsmsDbContext>().Database.Migrate();
            scope.ServiceProvider.GetRequiredService<AsmsIdentityDbContext>().Database.Migrate();
        }

        public ServiceProvider Services { get; }

        public IAuthService AuthService(IServiceScope scope) =>
            scope.ServiceProvider.GetRequiredService<IAuthService>();

        public async Task<Account> CreateAccountAsync(
            Guid? schoolId = null,
            string? userName = null,
            bool isUserActive = true,
            bool isSchoolActive = true,
            bool isSchoolDeleted = false,
            bool includeRole = true,
            params RoleSetup[] extraRoles)
        {
            var accountSchoolId = schoolId ?? Guid.NewGuid();
            var profileId = Guid.NewGuid();
            var roleName = $"Role-{Guid.NewGuid():N}";
            var accountUserName = userName ?? CreateUserName();
            var password = CreatePassword();
            var profile = new AppUser
            {
                Id = profileId,
                SchoolId = accountSchoolId,
                FullName = "Test account"
            };
            var schoolIds = new[] { accountSchoolId }
                .Concat(extraRoles.Select(role => role.SchoolId))
                .Distinct()
                .ToArray();

            using (var scope = Services.CreateScope())
            {
                var context = scope.ServiceProvider.GetRequiredService<AsmsDbContext>();
                context.Schools.AddRange(schoolIds.Select(id => new School
                {
                    Id = id,
                    Name = $"School-{id:N}",
                    IsActive = id == accountSchoolId ? isSchoolActive : true,
                    IsDeleted = id == accountSchoolId && isSchoolDeleted
                }));
                context.AppUsers.Add(profile);

                var roleSetups = extraRoles.ToList();
                if (includeRole)
                {
                    roleSetups.Insert(0, new RoleSetup(roleName, accountSchoolId));
                }

                foreach (var roleSetup in roleSetups)
                {
                    var roleId = Guid.NewGuid();
                    context.AppRoles.Add(new AppRole
                    {
                        Id = roleId,
                        SchoolId = roleSetup.SchoolId,
                        Name = roleSetup.Name,
                        IsDeleted = roleSetup.IsDeleted
                    });
                    context.UserRoles.Add(new UserRole
                    {
                        SchoolId = accountSchoolId,
                        UserId = profileId,
                        RoleId = roleId
                    });
                }

                await context.SaveChangesAsync();
            }

            var identityUser = new AsmsIdentityUser
            {
                Id = Guid.NewGuid(),
                UserName = accountUserName,
                SchoolId = accountSchoolId,
                AppUserId = profileId,
                IsActive = isUserActive
            };
            using (var scope = Services.CreateScope())
            {
                var users = scope.ServiceProvider.GetRequiredService<UserManager<AsmsIdentityUser>>();
                var result = await users.CreateAsync(identityUser, password);
                Assert.True(result.Succeeded);
            }

            return new Account(accountSchoolId, accountUserName, password, roleName, identityUser);
        }

        public void Dispose()
        {
            Services.Dispose();
            _connection.Dispose();
        }
    }
}
