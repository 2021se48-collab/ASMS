using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text.Json;
using Asms.Api.Authorization;
using Asms.Application.Abstractions;
using Asms.Application.Auth;
using Asms.Application.Security;
using Asms.Contracts.Auth;
using Asms.Domain.Security;
using Asms.Domain.Schools;
using Asms.Infrastructure.Identity;
using Asms.Infrastructure.Persistence;
using Asms.Infrastructure.Persistence.Seeding;
using Asms.Infrastructure.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authorization.Infrastructure;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace Asms.Api.Tests.Authorization;

public class PermissionAuthorizationTests
{
    [Fact]
    public async Task Permission_policies_return_expected_codes_without_revealing_access_details()
    {
        await using var factory = new PermissionWebApplicationFactory();
        using var client = factory.CreateClient();
        await factory.MigrateAsync();
        var parent = await factory.CreateAccountAsync(SystemRoles.Parent);
        var accountant = await factory.CreateAccountAsync(SystemRoles.Accountant);
        var principal = await factory.CreateAccountAsync(SystemRoles.SchoolPrincipal);
        var teacher = await factory.CreateAccountAsync(SystemRoles.Teacher);
        var owner = await factory.CreateAccountAsync(SystemRoles.SchoolOwner);
        await factory.RemoveRolePermissionAsync(
            principal.SchoolId,
            SystemRoles.SchoolPrincipal,
            Permissions.Audit.View);

        using (var anonymous = await client.GetAsync("/test/permissions/students"))
        {
            await AssertErrorAsync(
                anonymous,
                HttpStatusCode.Unauthorized,
                "UNAUTHENTICATED",
                Permissions.Students.View,
                SystemRoles.SchoolOwner);
        }

        await AssertPermissionDeniedAsync(
            client,
            await factory.LoginAsync(client, parent),
            "/test/permissions/students",
            Permissions.Students.View,
            SystemRoles.Parent);
        await AssertPermissionDeniedAsync(
            client,
            await factory.LoginAsync(client, accountant),
            "/test/permissions/reverse-fees",
            Permissions.Fees.Reverse,
            SystemRoles.Accountant);
        await AssertPermissionDeniedAsync(
            client,
            await factory.LoginAsync(client, principal),
            "/test/permissions/audit",
            Permissions.Audit.View,
            SystemRoles.SchoolPrincipal);
        await AssertPermissionDeniedAsync(
            client,
            await factory.LoginAsync(client, teacher),
            "/test/permissions/reverse-fees",
            Permissions.Fees.Reverse,
            SystemRoles.Teacher);

        using var teacherResponse = await SendAuthorizedAsync(
            client,
            await factory.LoginAsync(client, teacher),
            "/test/permissions/students");
        using var principalResponse = await SendAuthorizedAsync(
            client,
            await factory.LoginAsync(client, principal),
            "/test/permissions/reverse-fees");
        using var ownerResponse = await SendAuthorizedAsync(
            client,
            await factory.LoginAsync(client, owner),
            "/test/permissions/audit");

        Assert.Equal(HttpStatusCode.OK, teacherResponse.StatusCode);
        Assert.Equal(HttpStatusCode.OK, principalResponse.StatusCode);
        Assert.Equal(HttpStatusCode.OK, ownerResponse.StatusCode);
    }

    [Fact]
    public async Task Deactivating_an_account_or_removing_its_role_invalidates_an_existing_token()
    {
        await using var factory = new PermissionWebApplicationFactory();
        using var client = factory.CreateClient();
        await factory.MigrateAsync();
        var inactiveAccount = await factory.CreateAccountAsync(SystemRoles.Teacher);
        var inactiveToken = await factory.LoginAsync(client, inactiveAccount);
        await factory.SetIdentityActiveAsync(inactiveAccount.IdentityUserId, false);

        using var inactiveResponse = await SendAuthorizedAsync(
            client,
            inactiveToken,
            "/test/permissions/students");
        await AssertErrorAsync(
            inactiveResponse,
            HttpStatusCode.Forbidden,
            "FORBIDDEN",
            Permissions.Students.View,
            SystemRoles.Teacher);

        using var inactivePermissionsResponse = await SendAuthorizedAsync(
            client,
            inactiveToken,
            "/api/auth/permissions");
        Assert.True(inactivePermissionsResponse.Headers.CacheControl?.NoStore);
        await AssertErrorAsync(
            inactivePermissionsResponse,
            HttpStatusCode.Forbidden,
            "FORBIDDEN",
            Permissions.Students.View,
            SystemRoles.Teacher);

        var removedRoleAccount = await factory.CreateAccountAsync(SystemRoles.Teacher);
        var removedRoleToken = await factory.LoginAsync(client, removedRoleAccount);
        await factory.RemoveUserRolesAsync(removedRoleAccount.AppUserId);

        using var removedRoleResponse = await SendAuthorizedAsync(
            client,
            removedRoleToken,
            "/test/permissions/students");
        await AssertErrorAsync(
            removedRoleResponse,
            HttpStatusCode.Forbidden,
            "FORBIDDEN",
            Permissions.Students.View,
            SystemRoles.Teacher);
    }

    [Fact]
    public async Task Permission_checks_ignore_token_school_and_role_claims()
    {
        await using var factory = new PermissionWebApplicationFactory();
        using var client = factory.CreateClient();
        await factory.MigrateAsync();
        var student = await factory.CreateAccountAsync(SystemRoles.Student);
        var tokenService = new JwtTokenService(Options.Create(factory.JwtOptions));

        var forgedRoleToken = tokenService.CreateAccessToken(
            student.IdentityUserId,
            student.SchoolId,
            [SystemRoles.SchoolOwner],
            DateTime.UtcNow).Token;
        using var forgedRoleResponse = await SendAuthorizedAsync(
            client,
            forgedRoleToken,
            "/test/permissions/audit");
        await AssertErrorAsync(
            forgedRoleResponse,
            HttpStatusCode.Forbidden,
            "FORBIDDEN",
            Permissions.Audit.View,
            SystemRoles.SchoolOwner);

        var wrongSchoolToken = tokenService.CreateAccessToken(
            student.IdentityUserId,
            Guid.NewGuid(),
            [SystemRoles.SchoolOwner],
            DateTime.UtcNow).Token;
        using var wrongSchoolResponse = await SendAuthorizedAsync(
            client,
            wrongSchoolToken,
            "/test/permissions/audit");
        await AssertErrorAsync(
            wrongSchoolResponse,
            HttpStatusCode.Forbidden,
            "FORBIDDEN",
            Permissions.Audit.View,
            SystemRoles.SchoolOwner);
    }

    [Fact]
    public async Task Permissions_endpoint_is_private_sorted_and_limited_to_the_current_school()
    {
        await using var factory = new PermissionWebApplicationFactory();
        using var client = factory.CreateClient();
        await factory.MigrateAsync();

        using (var anonymous = await client.GetAsync("/api/auth/permissions"))
        {
            await AssertErrorAsync(
                anonymous,
                HttpStatusCode.Unauthorized,
                "UNAUTHENTICATED",
                Permissions.Students.View,
                SystemRoles.Teacher);
        }

        var teacher = await factory.CreateAccountAsync(SystemRoles.Teacher);
        var teacherToken = await factory.LoginAsync(client, teacher);
        using var teacherResponse = await SendAuthorizedAsync(
            client,
            teacherToken,
            "/api/auth/permissions");
        Assert.Equal(HttpStatusCode.OK, teacherResponse.StatusCode);
        Assert.True(teacherResponse.Headers.CacheControl?.NoStore);
        using var teacherDocument = JsonDocument.Parse(
            await teacherResponse.Content.ReadAsStringAsync());
        var teacherRoot = teacherDocument.RootElement;
        Assert.Equal(
            [SystemRoles.Teacher],
            teacherRoot.GetProperty("roles").EnumerateArray()
                .Select(item => item.GetString()!)
                .ToArray());
        Assert.Equal(
            DefaultRolePermissions.ForRole(SystemRoles.Teacher)
                .OrderBy(value => value, StringComparer.Ordinal),
            teacherRoot.GetProperty("permissions").EnumerateArray()
                .Select(item => item.GetString()!)
                .ToArray());

        var accountant = await factory.CreateAccountAsync(
            SystemRoles.Accountant,
            Guid.NewGuid());
        var accountantToken = await factory.LoginAsync(client, accountant);
        using var accountantResponse = await SendAuthorizedAsync(
            client,
            accountantToken,
            "/api/auth/permissions");
        Assert.Equal(HttpStatusCode.OK, accountantResponse.StatusCode);
        using var accountantDocument = JsonDocument.Parse(
            await accountantResponse.Content.ReadAsStringAsync());
        Assert.Equal(
            [SystemRoles.Accountant],
            accountantDocument.RootElement.GetProperty("roles").EnumerateArray()
                .Select(item => item.GetString()!)
                .ToArray());
        Assert.Equal(
            DefaultRolePermissions.ForRole(SystemRoles.Accountant)
                .OrderBy(value => value, StringComparer.Ordinal),
            accountantDocument.RootElement.GetProperty("permissions").EnumerateArray()
                .Select(item => item.GetString()!)
                .ToArray());
        Assert.DoesNotContain(
            Permissions.Attendance.View,
            accountantDocument.RootElement.GetProperty("permissions").EnumerateArray()
                .Select(item => item.GetString()!));
    }

    [Fact]
    public async Task Health_and_login_remain_available()
    {
        await using var factory = new PermissionWebApplicationFactory();
        using var client = factory.CreateClient();
        await factory.MigrateAsync();
        var account = await factory.CreateAccountAsync(SystemRoles.Teacher);

        using var health = await client.GetAsync("/api/health");
        using var login = await LoginRequestAsync(client, account);

        Assert.Equal(HttpStatusCode.OK, health.StatusCode);
        Assert.Equal(HttpStatusCode.OK, login.StatusCode);
    }

    [Fact]
    public async Task Permission_attribute_validates_names_and_policy_provider_resolves_only_permission_policies()
    {
        Assert.Throws<ArgumentException>(() => new HasPermissionAttribute("unknown.permission"));

        var provider = new PermissionPolicyProvider(Options.Create(new AuthorizationOptions()));
        var permissionPolicy = await provider.GetPolicyAsync("perm:Students.View");
        var otherPolicy = await provider.GetPolicyAsync("unknown-policy");

        Assert.NotNull(permissionPolicy);
        Assert.Contains(
            permissionPolicy.Requirements,
            requirement => requirement is PermissionRequirement
                { Permission: Permissions.Students.View });
        Assert.Contains(
            permissionPolicy.Requirements,
            requirement => requirement is DenyAnonymousAuthorizationRequirement);
        Assert.Null(otherPolicy);
    }

    private static async Task AssertPermissionDeniedAsync(
        HttpClient client,
        string token,
        string path,
        string permission,
        string role)
    {
        using var response = await SendAuthorizedAsync(client, token, path);
        await AssertErrorAsync(
            response,
            HttpStatusCode.Forbidden,
            "FORBIDDEN",
            permission,
            role);
    }

    private static async Task AssertErrorAsync(
        HttpResponseMessage response,
        HttpStatusCode status,
        string code,
        params string[] hiddenValues)
    {
        Assert.Equal(status, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        using var document = JsonDocument.Parse(body);
        Assert.Equal(code, document.RootElement.GetProperty("code").GetString());
        Assert.All(hiddenValues, value => Assert.DoesNotContain(value, body, StringComparison.Ordinal));
    }

    private static async Task<HttpResponseMessage> SendAuthorizedAsync(
        HttpClient client,
        string token,
        string path)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, path);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return await client.SendAsync(request);
    }

    private static Task<HttpResponseMessage> LoginRequestAsync(
        HttpClient client,
        TestAccount account) =>
        client.PostAsJsonAsync(
            "/api/auth/login",
            new LoginRequest(account.UserName, account.Password));

    private sealed record TestAccount(
        Guid SchoolId,
        Guid IdentityUserId,
        Guid AppUserId,
        string UserName,
        string Password);

    private sealed class PermissionWebApplicationFactory : WebApplicationFactory<Program>
    {
        private readonly SqliteConnection _connection;
        private readonly string _connectionString;

        public PermissionWebApplicationFactory()
        {
            SigningKey = Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
            JwtOptions = new JwtOptions { SigningKey = SigningKey };
            var dataSource = Guid.NewGuid().ToString("N");
            _connection = new SqliteConnection(new SqliteConnectionStringBuilder
            {
                DataSource = dataSource,
                Mode = SqliteOpenMode.Memory,
                Cache = SqliteCacheMode.Shared
            }.ToString());
            _connection.Open();
            _connectionString = new SqliteConnectionStringBuilder
            {
                DataSource = dataSource,
                Mode = SqliteOpenMode.Memory,
                Cache = SqliteCacheMode.Shared
            }.ToString();
        }

        public string SigningKey { get; }

        public JwtOptions JwtOptions { get; }

        public async Task MigrateAsync()
        {
            var mainOptions = new DbContextOptionsBuilder<AsmsDbContext>()
                .UseSqlite(
                    _connection,
                    sqlite => sqlite.MigrationsAssembly("Asms.Infrastructure.Migrations.Sqlite"))
                .Options;
            await using var mainContext = new AsmsDbContext(mainOptions, new TestTenant());
            await mainContext.Database.MigrateAsync();

            var identityOptions = new DbContextOptionsBuilder<AsmsIdentityDbContext>()
                .UseSqlite(
                    _connection,
                    sqlite => sqlite.MigrationsAssembly("Asms.Infrastructure.Migrations.Sqlite"))
                .Options;
            await using var identityContext = new AsmsIdentityDbContext(identityOptions);
            await identityContext.Database.MigrateAsync();
        }

        public async Task<TestAccount> CreateAccountAsync(
            string roleName,
            Guid? schoolId = null)
        {
            var actualSchoolId = schoolId ?? Guid.NewGuid();
            var appUserId = Guid.NewGuid();
            var userName = $"user-{Guid.NewGuid():N}";
            var password = $"Aa1{Guid.NewGuid():N}";

            using var scope = Services.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<AsmsDbContext>();
            if (!await context.Schools.IgnoreQueryFilters()
                    .AnyAsync(school => school.Id == actualSchoolId))
            {
                context.Schools.Add(new School
                {
                    Id = actualSchoolId,
                    Name = $"School {actualSchoolId:N}"
                });
                await context.SaveChangesAsync();
            }

            await new SchoolRoleSeeder(
                    context,
                    new TestTenant { SchoolId = actualSchoolId })
                .SeedAsync(actualSchoolId);

            var role = await context.AppRoles.IgnoreQueryFilters()
                .SingleAsync(item =>
                    item.SchoolId == actualSchoolId &&
                    item.Name == roleName);
            context.AppUsers.Add(new AppUser
            {
                Id = appUserId,
                SchoolId = actualSchoolId,
                FullName = "Test account",
                IdentityUserId = null
            });
            context.UserRoles.Add(new UserRole
            {
                SchoolId = actualSchoolId,
                UserId = appUserId,
                RoleId = role.Id
            });
            await context.SaveChangesAsync();

            var identityUser = new AsmsIdentityUser
            {
                Id = Guid.NewGuid(),
                UserName = userName,
                SchoolId = actualSchoolId,
                AppUserId = appUserId
            };
            var result = await scope.ServiceProvider
                .GetRequiredService<UserManager<AsmsIdentityUser>>()
                .CreateAsync(identityUser, password);
            Assert.True(result.Succeeded);

            return new TestAccount(
                actualSchoolId,
                identityUser.Id,
                appUserId,
                userName,
                password);
        }

        public async Task<string> LoginAsync(HttpClient client, TestAccount account)
        {
            using var response = await LoginRequestAsync(client, account);
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            var login = await response.Content.ReadFromJsonAsync<LoginResponse>();
            Assert.NotNull(login);
            return login.AccessToken;
        }

        public async Task SetIdentityActiveAsync(Guid userId, bool isActive)
        {
            using var scope = Services.CreateScope();
            var identityDb = scope.ServiceProvider
                .GetRequiredService<AsmsIdentityDbContext>();
            var user = await identityDb.Users.SingleAsync(item => item.Id == userId);
            user.IsActive = isActive;
            await identityDb.SaveChangesAsync();
        }

        public async Task RemoveUserRolesAsync(Guid appUserId)
        {
            using var scope = Services.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<AsmsDbContext>();
            var roles = await context.UserRoles.IgnoreQueryFilters()
                .Where(item => item.UserId == appUserId)
                .ToListAsync();
            foreach (var role in roles)
            {
                role.IsDeleted = true;
            }

            await context.SaveChangesAsync();
        }

        public async Task RemoveRolePermissionAsync(
            Guid schoolId,
            string roleName,
            string permission)
        {
            using var scope = Services.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<AsmsDbContext>();
            var role = await context.AppRoles.IgnoreQueryFilters()
                .SingleAsync(item => item.SchoolId == schoolId && item.Name == roleName);
            var row = await context.RolePermissions.IgnoreQueryFilters()
                .SingleAsync(item =>
                    item.SchoolId == schoolId &&
                    item.RoleId == role.Id &&
                    item.Permission == permission);
            row.IsDeleted = true;
            await context.SaveChangesAsync();
        }

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment("Testing");
            builder.ConfigureAppConfiguration((_, configuration) =>
            {
                configuration.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["ConnectionStrings:Default"] = _connectionString,
                    ["Database:Provider"] = "Sqlite",
                    ["Jwt:SigningKey"] = SigningKey
                });
            });
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<AsmsDbContext>();
                services.RemoveAll<AsmsIdentityDbContext>();
                services.RemoveAll<DbContextOptions<AsmsDbContext>>();
                services.RemoveAll<DbContextOptions<AsmsIdentityDbContext>>();
                services.RemoveAll(typeof(DbContextOptions));
                RemoveOptionsConfiguration<AsmsDbContext>(services);
                RemoveOptionsConfiguration<AsmsIdentityDbContext>(services);

                services.AddDbContext<AsmsDbContext>(options =>
                    options.UseSqlite(
                        _connection,
                        sqlite => sqlite.MigrationsAssembly("Asms.Infrastructure.Migrations.Sqlite")));
                services.AddDbContext<AsmsIdentityDbContext>(options =>
                    options.UseSqlite(
                        _connection,
                        sqlite => sqlite.MigrationsAssembly("Asms.Infrastructure.Migrations.Sqlite")));
                services.AddControllers()
                    .AddApplicationPart(typeof(TestPermissionController).Assembly);
            });
        }

        private static void RemoveOptionsConfiguration<TContext>(IServiceCollection services)
            where TContext : DbContext
        {
            for (var index = services.Count - 1; index >= 0; index--)
            {
                var serviceType = services[index].ServiceType;
                if (serviceType.IsGenericType &&
                    serviceType.GetGenericTypeDefinition().Name == "IDbContextOptionsConfiguration`1" &&
                    serviceType.GenericTypeArguments[0] == typeof(TContext))
                {
                    services.RemoveAt(index);
                }
            }
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _connection.Dispose();
            }

            base.Dispose(disposing);
        }
    }

    private sealed class TestTenant : ICurrentTenant
    {
        public Guid? SchoolId { get; set; }

        public Guid? UserId { get; set; }

        public Guid? DeviceId { get; set; }
    }
}

[ApiController]
[Route("test/permissions")]
public sealed class TestPermissionController : ControllerBase
{
    [HttpGet("students")]
    [HasPermission(Permissions.Students.View)]
    public IActionResult Students() => Ok();

    [HttpGet("reverse-fees")]
    [HasPermission(Permissions.Fees.Reverse)]
    public IActionResult ReverseFees() => Ok();

    [HttpGet("audit")]
    [HasPermission(Permissions.Audit.View)]
    public IActionResult Audit() => Ok();
}
