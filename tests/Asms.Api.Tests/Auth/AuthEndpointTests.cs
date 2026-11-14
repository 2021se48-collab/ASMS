using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text.Json;
using Asms.Application.Abstractions;
using Asms.Application.Security;
using Asms.Contracts.Auth;
using Asms.Domain.Security;
using Asms.Domain.Schools;
using Asms.Infrastructure.Identity;
using Asms.Infrastructure.Persistence;
using Asms.Infrastructure.Security;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.IdentityModel.Tokens;
using Microsoft.Data.Sqlite;
using Microsoft.AspNetCore.Identity;

namespace Asms.Api.Tests.Auth;

public class AuthEndpointTests
{
    [Fact]
    public async Task Login_returns_a_signed_token_and_no_store_header()
    {
        await using var factory = new AuthWebApplicationFactory();
        using var client = factory.CreateClient();
        await factory.MigrateAsync();
        var account = await factory.CreateAccountAsync();

        using var response = await LoginAsync(client, account.UserName, account.Password);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.True(response.Headers.CacheControl?.NoStore);
        var body = await response.Content.ReadAsStringAsync();
        using var document = JsonDocument.Parse(body);
        var root = document.RootElement;
        var accessToken = root.GetProperty("accessToken").GetString();
        Assert.False(string.IsNullOrWhiteSpace(accessToken));
        Assert.DoesNotContain(account.Password, body, StringComparison.Ordinal);
        Assert.DoesNotContain(factory.SigningKey, body, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Every_login_failure_returns_the_same_error_response()
    {
        await using var factory = new AuthWebApplicationFactory();
        using var client = factory.CreateClient();
        await factory.MigrateAsync();
        var active = await factory.CreateAccountAsync();
        var inactiveUser = await factory.CreateAccountAsync(isUserActive: false);
        var inactiveSchool = await factory.CreateAccountAsync(isSchoolActive: false);
        var deletedSchool = await factory.CreateAccountAsync(isSchoolDeleted: true);
        var noRoles = await factory.CreateAccountAsync(includeRole: false);

        var attempts = new[]
        {
            (active.UserName, CreatePassword()),
            (CreateUserName(), CreatePassword()),
            (inactiveUser.UserName, inactiveUser.Password),
            (inactiveSchool.UserName, inactiveSchool.Password),
            (deletedSchool.UserName, deletedSchool.Password),
            (noRoles.UserName, noRoles.Password)
        };
        string? expectedBody = null;

        foreach (var (userName, password) in attempts)
        {
            using var response = await LoginAsync(client, userName, password);
            Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
            Assert.True(response.Headers.CacheControl?.NoStore);
            var body = await response.Content.ReadAsStringAsync();
            using var document = JsonDocument.Parse(body);
            Assert.Equal("INVALID_CREDENTIALS", document.RootElement.GetProperty("code").GetString());
            expectedBody ??= body;
            Assert.Equal(expectedBody, body);
        }
    }

    [Fact]
    public async Task Blank_and_oversized_login_fields_return_invalid_request()
    {
        await using var factory = new AuthWebApplicationFactory();
        using var client = factory.CreateClient();
        await factory.MigrateAsync();

        var requests = new[]
        {
            new LoginRequest(" ", CreatePassword()),
            new LoginRequest(CreateUserName(), " "),
            new LoginRequest(new string('x', 257), CreatePassword()),
            new LoginRequest(CreateUserName(), new string('x', 257))
        };

        foreach (var request in requests)
        {
            using var response = await client.PostAsJsonAsync("/api/auth/login", request);
            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
            var error = await response.Content.ReadFromJsonAsync<ErrorResponse>();
            Assert.Equal("INVALID_REQUEST", error?.Code);
        }
    }

    [Fact]
    public async Task Me_requires_a_valid_token_and_returns_only_claimed_tenant_data()
    {
        await using var factory = new AuthWebApplicationFactory();
        using var client = factory.CreateClient();
        await factory.MigrateAsync();
        var accountA = await factory.CreateAccountAsync(roleName: "SchoolOwner-A");
        var accountB = await factory.CreateAccountAsync(roleName: "SchoolOwner-B");

        using var anonymousResponse = await client.GetAsync("/api/auth/me");
        Assert.Equal(HttpStatusCode.Unauthorized, anonymousResponse.StatusCode);

        using var loginResponse = await LoginAsync(client, accountA.UserName, accountA.Password);
        Assert.Equal(HttpStatusCode.OK, loginResponse.StatusCode);
        var loginJson = await loginResponse.Content.ReadFromJsonAsync<LoginResponse>();
        Assert.NotNull(loginJson);

        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", loginJson.AccessToken);
        using var meResponse = await client.GetAsync("/api/auth/me");

        Assert.Equal(HttpStatusCode.OK, meResponse.StatusCode);
        using var document = JsonDocument.Parse(await meResponse.Content.ReadAsStringAsync());
        var root = document.RootElement;
        Assert.Equal(accountA.IdentityUserId, root.GetProperty("userId").GetGuid());
        Assert.Equal(accountA.SchoolId, root.GetProperty("schoolId").GetGuid());
        Assert.Equal(
            ["SchoolOwner-A"],
            root.GetProperty("roles").EnumerateArray().Select(role => role.GetString()!).ToArray());
        Assert.DoesNotContain(
            "SchoolOwner-B",
            root.GetProperty("roles").EnumerateArray().Select(role => role.GetString()!));

        var parts = loginJson.AccessToken.Split('.');
        var firstSignatureChar = parts[2][0];
        parts[2] = (firstSignatureChar == 'A' ? "B" : "A") + parts[2][1..];
        using var tampered = await GetMeAsync(client, string.Join('.', parts));
        Assert.Equal(HttpStatusCode.Unauthorized, tampered.StatusCode);

        var expiredToken = new JwtTokenService(
            Microsoft.Extensions.Options.Options.Create(factory.JwtOptions))
            .CreateAccessToken(
                accountA.IdentityUserId,
                accountA.SchoolId,
                ["SchoolOwner-A"],
                DateTime.UtcNow.AddHours(-1))
            .Token;
        using var expired = await GetMeAsync(client, expiredToken);
        Assert.Equal(HttpStatusCode.Unauthorized, expired.StatusCode);

        var otherKeyOptions = new JwtOptions
        {
            SigningKey = CreateSigningKey(),
            Issuer = factory.JwtOptions.Issuer,
            Audience = factory.JwtOptions.Audience
        };
        var wrongKeyToken = new JwtTokenService(
            Microsoft.Extensions.Options.Options.Create(otherKeyOptions))
            .CreateAccessToken(
                accountA.IdentityUserId,
                accountA.SchoolId,
                ["SchoolOwner-A"],
                DateTime.UtcNow)
            .Token;
        using var wrongKey = await GetMeAsync(client, wrongKeyToken);
        Assert.Equal(HttpStatusCode.Unauthorized, wrongKey.StatusCode);
    }

    [Fact]
    public async Task Eleventh_login_request_from_one_client_is_rate_limited()
    {
        await using var factory = new AuthWebApplicationFactory();
        using var client = factory.CreateClient();
        await factory.MigrateAsync();

        for (var attempt = 0; attempt < 10; attempt++)
        {
            using var response = await LoginAsync(client, CreateUserName(), CreatePassword());
            Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        }

        using var limitedResponse = await LoginAsync(client, CreateUserName(), CreatePassword());
        Assert.Equal((HttpStatusCode)429, limitedResponse.StatusCode);
    }

    [Fact]
    public async Task Health_endpoint_remains_anonymous()
    {
        await using var factory = new AuthWebApplicationFactory();
        using var client = factory.CreateClient();

        using var response = await client.GetAsync("/api/health");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public void Host_does_not_start_without_a_signing_key()
    {
        using var factory = new AuthWebApplicationFactory(includeSigningKey: false);

        Assert.ThrowsAny<Exception>(() => factory.CreateClient());
    }

    [Fact]
    public void Host_does_not_start_with_a_short_signing_key()
    {
        using var factory = new AuthWebApplicationFactory(signingKey: new string('x', 8));

        Assert.ThrowsAny<Exception>(() => factory.CreateClient());
    }

    [Fact]
    public void Host_does_not_start_without_a_database_provider()
    {
        using var factory = new AuthWebApplicationFactory(includeDatabaseProvider: false);

        Assert.ThrowsAny<Exception>(() => factory.CreateClient());
    }

    private static Task<HttpResponseMessage> LoginAsync(
        HttpClient client,
        string userName,
        string password) =>
        client.PostAsJsonAsync("/api/auth/login", new LoginRequest(userName, password));

    private static async Task<HttpResponseMessage> GetMeAsync(HttpClient client, string token)
    {
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return await client.GetAsync("/api/auth/me");
    }

    private static string CreatePassword() => $"Aa1{Guid.NewGuid():N}";

    private static string CreateSigningKey() =>
        Convert.ToHexString(RandomNumberGenerator.GetBytes(32));

    private static string CreateUserName() => $"user-{Guid.NewGuid():N}";

    private sealed record SeededAccount(
        Guid SchoolId,
        Guid IdentityUserId,
        string UserName,
        string Password,
        string RoleName);

    private sealed class AuthWebApplicationFactory : WebApplicationFactory<Program>
    {
        private readonly SqliteConnection _connection;
        private readonly bool _includeSigningKey;
        private readonly bool _includeDatabaseProvider;

        public AuthWebApplicationFactory(
            bool includeSigningKey = true,
            string? signingKey = null,
            bool includeDatabaseProvider = true)
        {
            _includeSigningKey = includeSigningKey;
            _includeDatabaseProvider = includeDatabaseProvider;
            SigningKey = signingKey ?? CreateSigningKey();
            JwtOptions = new JwtOptions { SigningKey = SigningKey };

            var dataSource = Guid.NewGuid().ToString("N");
            _connection = new SqliteConnection(new SqliteConnectionStringBuilder
            {
                DataSource = dataSource,
                Mode = SqliteOpenMode.Memory,
                Cache = SqliteCacheMode.Shared
            }.ToString());
            _connection.Open();
            ConnectionString = new SqliteConnectionStringBuilder
            {
                DataSource = dataSource,
                Mode = SqliteOpenMode.Memory,
                Cache = SqliteCacheMode.Shared
            }.ToString();
        }

        public string SigningKey { get; }

        public JwtOptions JwtOptions { get; }

        private string ConnectionString { get; }

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

        public async Task<SeededAccount> CreateAccountAsync(
            bool isUserActive = true,
            bool isSchoolActive = true,
            bool isSchoolDeleted = false,
            bool includeRole = true,
            string? roleName = null)
        {
            var schoolId = Guid.NewGuid();
            var appUserId = Guid.NewGuid();
            var roleId = Guid.NewGuid();
            var userName = CreateUserName();
            var password = CreatePassword();
            var actualRoleName = roleName ?? $"Role-{Guid.NewGuid():N}";

            using (var scope = Services.CreateScope())
            {
                var context = scope.ServiceProvider.GetRequiredService<AsmsDbContext>();
                context.Schools.Add(new School
                {
                    Id = schoolId,
                    Name = $"School-{schoolId:N}",
                    IsActive = isSchoolActive,
                    IsDeleted = isSchoolDeleted
                });
                context.AppUsers.Add(new AppUser
                {
                    Id = appUserId,
                    SchoolId = schoolId,
                    FullName = "Test account"
                });

                if (includeRole)
                {
                    context.AppRoles.Add(new AppRole
                    {
                        Id = roleId,
                        SchoolId = schoolId,
                        Name = actualRoleName
                    });
                    context.UserRoles.Add(new UserRole
                    {
                        SchoolId = schoolId,
                        UserId = appUserId,
                        RoleId = roleId
                    });
                }

                await context.SaveChangesAsync();

                var identityUser = new AsmsIdentityUser
                {
                    Id = Guid.NewGuid(),
                    UserName = userName,
                    SchoolId = schoolId,
                    AppUserId = appUserId,
                    IsActive = isUserActive
                };
                var identityResult = await scope.ServiceProvider
                    .GetRequiredService<UserManager<AsmsIdentityUser>>()
                    .CreateAsync(identityUser, password);
                Assert.True(identityResult.Succeeded);

                return new SeededAccount(schoolId, identityUser.Id, userName, password, actualRoleName);
            }
        }

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment("Testing");
            builder.ConfigureAppConfiguration((_, configuration) =>
            {
                var settings = new Dictionary<string, string?>
                {
                    ["ConnectionStrings:Default"] = ConnectionString
                };
                if (_includeDatabaseProvider)
                {
                    settings["Database:Provider"] = "Sqlite";
                }

                if (_includeSigningKey)
                {
                    settings["Jwt:SigningKey"] = SigningKey;
                }

                configuration.AddInMemoryCollection(settings);
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

        private sealed class TestTenant : ICurrentTenant
        {
            public Guid? SchoolId => null;

            public Guid? UserId => null;

            public Guid? DeviceId => null;
        }
    }
}
