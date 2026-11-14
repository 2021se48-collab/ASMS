using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Asms.Application.Abstractions;
using Asms.Application.Security;
using Asms.Contracts.Auth;
using Asms.Domain.Security;
using Asms.Domain.Schools;
using Asms.Infrastructure.Identity;
using Asms.Infrastructure.Persistence;
using Asms.Infrastructure.Persistence.Seeding;
using Asms.Infrastructure.Security;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Asms.Api.Tests.Auth;

public class RefreshEndpointTests
{
    [Fact]
    public async Task Login_and_refresh_return_rotated_tokens_and_new_access_token_works()
    {
        await using var factory = new RefreshWebApplicationFactory();
        using var client = factory.CreateClient();
        await factory.MigrateAsync();
        var account = await factory.CreateAccountAsync();

        using var loginResponse = await LoginRequestAsync(client, account);
        Assert.Equal(HttpStatusCode.OK, loginResponse.StatusCode);
        Assert.True(loginResponse.Headers.CacheControl?.NoStore);
        var login = await loginResponse.Content.ReadFromJsonAsync<LoginResponse>();
        Assert.NotNull(login);
        Assert.False(string.IsNullOrWhiteSpace(login.RefreshToken));
        Assert.True(login.RefreshTokenExpiresAtUtc > DateTime.UtcNow);
        var loginBody = await loginResponse.Content.ReadAsStringAsync();
        Assert.False(loginBody.Contains(account.Password, StringComparison.Ordinal));
        Assert.False(loginBody.Contains(factory.SigningKey, StringComparison.Ordinal));

        using var refreshResponse = await RefreshRequestAsync(client, login.RefreshToken);
        Assert.Equal(HttpStatusCode.OK, refreshResponse.StatusCode);
        Assert.True(refreshResponse.Headers.CacheControl?.NoStore);
        var refreshed = await refreshResponse.Content.ReadFromJsonAsync<LoginResponse>();
        Assert.NotNull(refreshed);
        Assert.False(string.IsNullOrWhiteSpace(refreshed.RefreshToken));
        Assert.False(string.Equals(login.RefreshToken, refreshed.RefreshToken, StringComparison.Ordinal));
        var refreshBody = await refreshResponse.Content.ReadAsStringAsync();
        Assert.False(refreshBody.Contains(account.Password, StringComparison.Ordinal));
        Assert.False(refreshBody.Contains(factory.SigningKey, StringComparison.Ordinal));

        using var me = await GetMeAsync(client, refreshed.AccessToken);
        Assert.Equal(HttpStatusCode.OK, me.StatusCode);
        using var reused = await RefreshRequestAsync(client, login.RefreshToken);
        await AssertErrorAsync(reused, HttpStatusCode.Unauthorized, "INVALID_REFRESH_TOKEN");
        using var newestStillWorks = await RefreshRequestAsync(client, refreshed.RefreshToken);
        Assert.Equal(HttpStatusCode.OK, newestStillWorks.StatusCode);
    }

    [Fact]
    public async Task Refresh_failures_share_one_response_and_invalid_lengths_are_bad_requests()
    {
        await using var factory = new RefreshWebApplicationFactory();
        using var client = factory.CreateClient();
        await factory.MigrateAsync();
        var account = await factory.CreateAccountAsync();
        var login = await factory.LoginAsync(client, account);

        using var unknown = await RefreshRequestAsync(client, "unknown-token");
        await AssertErrorAsync(unknown, HttpStatusCode.Unauthorized, "INVALID_REFRESH_TOKEN");
        var expectedBody = await unknown.Content.ReadAsStringAsync();

        using var malformed = await RefreshRequestAsync(client, "not-base64url");
        await AssertErrorAsync(malformed, HttpStatusCode.Unauthorized, "INVALID_REFRESH_TOKEN");
        Assert.Equal(expectedBody, await malformed.Content.ReadAsStringAsync());

        await factory.ExpireRefreshTokenAsync(login.RefreshToken);
        using var expired = await RefreshRequestAsync(client, login.RefreshToken);
        await AssertErrorAsync(expired, HttpStatusCode.Unauthorized, "INVALID_REFRESH_TOKEN");
        Assert.Equal(expectedBody, await expired.Content.ReadAsStringAsync());

        var invalidRequests = new[]
        {
            new RefreshRequest(string.Empty),
            new RefreshRequest(" "),
            new RefreshRequest(new string('x', 201))
        };
        foreach (var request in invalidRequests)
        {
            using var response = await client.PostAsJsonAsync("/api/auth/refresh", request);
            await AssertErrorAsync(response, HttpStatusCode.BadRequest, "INVALID_REQUEST");
        }
    }

    [Fact]
    public async Task Logout_returns_no_content_for_valid_and_unknown_tokens_and_revokes_the_family()
    {
        await using var factory = new RefreshWebApplicationFactory();
        using var client = factory.CreateClient();
        await factory.MigrateAsync();
        var account = await factory.CreateAccountAsync();
        var login = await factory.LoginAsync(client, account);

        using var unknownLogout = await client.PostAsJsonAsync(
            "/api/auth/logout",
            new LogoutRequest("unknown-token"));
        Assert.Equal(HttpStatusCode.NoContent, unknownLogout.StatusCode);
        Assert.True(unknownLogout.Headers.CacheControl?.NoStore);

        using var logout = await client.PostAsJsonAsync(
            "/api/auth/logout",
            new LogoutRequest(login.RefreshToken));
        Assert.Equal(HttpStatusCode.NoContent, logout.StatusCode);
        Assert.True(logout.Headers.CacheControl?.NoStore);

        using var refresh = await RefreshRequestAsync(client, login.RefreshToken);
        await AssertErrorAsync(refresh, HttpStatusCode.Unauthorized, "INVALID_REFRESH_TOKEN");
    }

    [Fact]
    public async Task Logout_all_requires_auth_and_revokes_only_the_callers_sessions()
    {
        await using var factory = new RefreshWebApplicationFactory();
        using var client = factory.CreateClient();
        await factory.MigrateAsync();
        var firstAccount = await factory.CreateAccountAsync();
        var secondAccount = await factory.CreateAccountAsync();
        var firstSession = await factory.LoginAsync(client, firstAccount);
        var secondSession = await factory.LoginAsync(client, firstAccount);
        var otherSession = await factory.LoginAsync(client, secondAccount);

        using var anonymous = await client.PostAsync("/api/auth/logout-all", content: null);
        await AssertErrorAsync(anonymous, HttpStatusCode.Unauthorized, "UNAUTHENTICATED");

        using var logoutAll = await SendAuthorizedAsync(
            client,
            HttpMethod.Post,
            "/api/auth/logout-all",
            firstSession.AccessToken);
        Assert.Equal(HttpStatusCode.NoContent, logoutAll.StatusCode);
        Assert.True(logoutAll.Headers.CacheControl?.NoStore);

        using var firstRefresh = await RefreshRequestAsync(client, firstSession.RefreshToken);
        using var secondRefresh = await RefreshRequestAsync(client, secondSession.RefreshToken);
        using var otherRefresh = await RefreshRequestAsync(client, otherSession.RefreshToken);
        await AssertErrorAsync(firstRefresh, HttpStatusCode.Unauthorized, "INVALID_REFRESH_TOKEN");
        await AssertErrorAsync(secondRefresh, HttpStatusCode.Unauthorized, "INVALID_REFRESH_TOKEN");
        Assert.Equal(HttpStatusCode.OK, otherRefresh.StatusCode);
    }

    [Fact]
    public async Task Refresh_rate_limit_returns_429_on_the_thirty_first_request()
    {
        await using var factory = new RefreshWebApplicationFactory();
        using var client = factory.CreateClient();
        await factory.MigrateAsync();

        for (var request = 0; request < 30; request++)
        {
            using var response = await RefreshRequestAsync(client, "unknown-token");
            Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        }

        using var limited = await RefreshRequestAsync(client, "unknown-token");
        Assert.Equal((HttpStatusCode)429, limited.StatusCode);
    }

    [Fact]
    public async Task Login_me_permissions_and_health_still_work()
    {
        await using var factory = new RefreshWebApplicationFactory();
        using var client = factory.CreateClient();
        await factory.MigrateAsync();
        var account = await factory.CreateAccountAsync();
        var login = await factory.LoginAsync(client, account);

        using var me = await GetMeAsync(client, login.AccessToken);
        using var permissions = await SendAuthorizedAsync(
            client,
            HttpMethod.Get,
            "/api/auth/permissions",
            login.AccessToken);
        using var health = await client.GetAsync("/api/health");

        Assert.Equal(HttpStatusCode.OK, me.StatusCode);
        Assert.Equal(HttpStatusCode.OK, permissions.StatusCode);
        Assert.Equal(HttpStatusCode.OK, health.StatusCode);
    }

    private static Task<HttpResponseMessage> LoginRequestAsync(
        HttpClient client,
        TestAccount account) =>
        client.PostAsJsonAsync("/api/auth/login", new LoginRequest(account.UserName, account.Password));

    private static Task<HttpResponseMessage> RefreshRequestAsync(
        HttpClient client,
        string token) =>
        client.PostAsJsonAsync("/api/auth/refresh", new RefreshRequest(token));

    private static async Task<HttpResponseMessage> GetMeAsync(HttpClient client, string accessToken) =>
        await SendAuthorizedAsync(client, HttpMethod.Get, "/api/auth/me", accessToken);

    private static async Task<HttpResponseMessage> SendAuthorizedAsync(
        HttpClient client,
        HttpMethod method,
        string path,
        string accessToken)
    {
        using var request = new HttpRequestMessage(method, path);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        return await client.SendAsync(request);
    }

    private static async Task AssertErrorAsync(
        HttpResponseMessage response,
        HttpStatusCode status,
        string code)
    {
        Assert.Equal(status, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        using var document = JsonDocument.Parse(body);
        Assert.Equal(code, document.RootElement.GetProperty("code").GetString());
    }

    private sealed record TestAccount(
        Guid SchoolId,
        Guid IdentityUserId,
        Guid AppUserId,
        string UserName,
        string Password);

    private sealed class RefreshWebApplicationFactory : WebApplicationFactory<Program>
    {
        private readonly SqliteConnection _connection;
        private readonly string _connectionString;

        public RefreshWebApplicationFactory()
        {
            SigningKey = Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
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

        public async Task<TestAccount> CreateAccountAsync()
        {
            var schoolId = Guid.NewGuid();
            var appUserId = Guid.NewGuid();
            var userName = $"user-{Guid.NewGuid():N}";
            var password = $"Aa1{Guid.NewGuid():N}";
            var identityUserId = Guid.NewGuid();

            using var scope = Services.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<AsmsDbContext>();
            context.Schools.Add(new School
            {
                Id = schoolId,
                Name = $"School-{schoolId:N}"
            });
            await context.SaveChangesAsync();
            await new SchoolRoleSeeder(context, new TestTenant { SchoolId = schoolId })
                .SeedAsync(schoolId);
            var teacherRole = await context.AppRoles.IgnoreQueryFilters()
                .SingleAsync(role =>
                    role.SchoolId == schoolId &&
                    role.Name == SystemRoles.Teacher);
            context.AppUsers.Add(new AppUser
            {
                Id = appUserId,
                SchoolId = schoolId,
                IdentityUserId = identityUserId,
                FullName = "Test account"
            });
            context.UserRoles.Add(new UserRole
            {
                SchoolId = schoolId,
                UserId = appUserId,
                RoleId = teacherRole.Id
            });
            await context.SaveChangesAsync();

            var user = new AsmsIdentityUser
            {
                Id = identityUserId,
                UserName = userName,
                SchoolId = schoolId,
                AppUserId = appUserId
            };
            var createResult = await scope.ServiceProvider
                .GetRequiredService<UserManager<AsmsIdentityUser>>()
                .CreateAsync(user, password);
            Assert.True(createResult.Succeeded);

            return new TestAccount(schoolId, identityUserId, appUserId, userName, password);
        }

        public async Task<LoginResponse> LoginAsync(HttpClient client, TestAccount account)
        {
            using var response = await LoginRequestAsync(client, account);
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            var login = await response.Content.ReadFromJsonAsync<LoginResponse>();
            Assert.NotNull(login);
            return login;
        }

        public async Task ExpireRefreshTokenAsync(string token)
        {
            using var scope = Services.CreateScope();
            var identityDb = scope.ServiceProvider.GetRequiredService<AsmsIdentityDbContext>();
            var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token)));
            var stored = await identityDb.RefreshTokens.SingleAsync(item => item.TokenHash == hash);
            stored.ExpiresAtUtc = DateTime.UtcNow.AddSeconds(-1);
            await identityDb.SaveChangesAsync();
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
            public Guid? SchoolId { get; set; }

            public Guid? UserId { get; set; }

            public Guid? DeviceId { get; set; }
        }
    }
}
