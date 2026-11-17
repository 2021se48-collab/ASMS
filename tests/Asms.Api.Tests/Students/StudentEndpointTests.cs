using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Cryptography;
using Asms.Application.Abstractions;
using Asms.Contracts.Auth;
using Asms.Contracts.Students;
using Asms.Domain.Enums;
using Asms.Domain.Schools;
using Asms.Domain.Security;
using Asms.Domain.Students;
using Asms.Infrastructure.Identity;
using Asms.Infrastructure.Persistence;
using Asms.Infrastructure.Persistence.Seeding;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.Identity;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Asms.Api.Tests.Students;

public sealed class StudentEndpointTests
{
    [Fact]
    public async Task Student_endpoints_require_authentication_and_return_no_store()
    {
        await using var factory = await StudentApiFactory.CreateAsync();
        using var client = factory.CreateClient();

        using var response = await client.GetAsync("/api/students");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.True(response.Headers.CacheControl?.NoStore);
        Assert.Equal(
            "UNAUTHENTICATED",
            (await response.Content.ReadFromJsonAsync<ErrorResponse>())?.Code);
    }

    [Fact]
    public async Task Accountant_can_read_but_cannot_create_students()
    {
        await using var factory = await StudentApiFactory.CreateAsync();
        using var client = factory.CreateClient();
        var accountant = await factory.CreateAccountAsync(SystemRoles.Accountant);
        await factory.AuthenticateAsync(client, accountant);

        using var get = await client.GetAsync("/api/students");
        using var post = await client.PostAsJsonAsync("/api/students", NewRequest());

        Assert.Equal(HttpStatusCode.OK, get.StatusCode);
        Assert.True(get.Headers.CacheControl?.NoStore);
        Assert.Equal(HttpStatusCode.Forbidden, post.StatusCode);
        Assert.True(post.Headers.CacheControl?.NoStore);
        Assert.Equal("FORBIDDEN", (await post.Content.ReadFromJsonAsync<ErrorResponse>())?.Code);
    }

    [Fact]
    public async Task Owner_can_create_read_and_confirm_a_duplicate()
    {
        await using var factory = await StudentApiFactory.CreateAsync();
        using var client = factory.CreateClient();
        var owner = await factory.CreateAccountAsync(SystemRoles.SchoolOwner);
        await factory.AuthenticateAsync(client, owner);

        using var created = await client.PostAsJsonAsync("/api/students", NewRequest(firstName: "علي"));
        var student = await created.Content.ReadFromJsonAsync<StudentDto>();
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        Assert.True(created.Headers.CacheControl?.NoStore);
        Assert.NotNull(student);
        Assert.Equal("DEMO1-2026-0001", student.StudentNumber);

        using var read = await client.GetAsync($"/api/students/{student.Id}");
        Assert.Equal(HttpStatusCode.OK, read.StatusCode);
        Assert.True(read.Headers.CacheControl?.NoStore);
        Assert.Equal(student.Id, (await read.Content.ReadFromJsonAsync<StudentDto>())?.Id);

        using var warning = await client.PostAsJsonAsync(
            "/api/students",
            NewRequest(firstName: "علی"));
        Assert.Equal(HttpStatusCode.Conflict, warning.StatusCode);
        var warningBody = await warning.Content.ReadFromJsonAsync<DuplicateWarningBody>();
        Assert.Equal("STUDENT_DUPLICATE_WARNING", warningBody?.Code);
        Assert.Contains(warningBody!.Duplicates, match => match.Id == student.Id);

        using var confirmed = await client.PostAsJsonAsync(
            "/api/students",
            NewRequest(firstName: "علی", confirmDuplicate: true));
        Assert.Equal(HttpStatusCode.Created, confirmed.StatusCode);
    }

    [Fact]
    public async Task Validation_error_is_a_code_only_response()
    {
        await using var factory = await StudentApiFactory.CreateAsync();
        using var client = factory.CreateClient();
        var owner = await factory.CreateAccountAsync(SystemRoles.SchoolOwner);
        await factory.AuthenticateAsync(client, owner);

        using var response = await client.PostAsJsonAsync("/api/students", NewRequest(firstName: " "));
        var body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.True(response.Headers.CacheControl?.NoStore);
        Assert.Equal("STUDENT_REQUIRED_FIELD", (await response.Content.ReadFromJsonAsync<ErrorResponse>())?.Code);
        Assert.DoesNotContain("Private address", body);
        Assert.DoesNotContain("+93700123456", body);
        Assert.DoesNotContain("stackTrace", body, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Search_and_get_never_return_students_from_another_school()
    {
        await using var factory = await StudentApiFactory.CreateAsync();
        using var client = factory.CreateClient();
        var owner = await factory.CreateAccountAsync(SystemRoles.SchoolOwner);
        var otherStudentId = await factory.CreateOtherSchoolStudentAsync();
        await factory.AuthenticateAsync(client, owner);

        using var response = await client.GetAsync("/api/students");
        var page = await response.Content.ReadFromJsonAsync<PagedResult<StudentListItemDto>>();
        using var otherStudent = await client.GetAsync($"/api/students/{otherStudentId}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Empty(page!.Items);
        Assert.DoesNotContain(otherStudentId.ToString(), await response.Content.ReadAsStringAsync());
        Assert.Equal(HttpStatusCode.NotFound, otherStudent.StatusCode);
        Assert.Equal("STUDENT_NOT_FOUND", (await otherStudent.Content.ReadFromJsonAsync<ErrorResponse>())?.Code);

        using var duplicate = await client.PostAsJsonAsync(
            "/api/students",
            NewRequest(firstName: "Isolated"));
        Assert.Equal(HttpStatusCode.Created, duplicate.StatusCode);
    }

    private static CreateStudentRequest NewRequest(
        string firstName = "Isolated",
        bool confirmDuplicate = false) =>
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
            [new GuardianLinkDto(
                null,
                "Guardian",
                "+93700999999",
                null,
                nameof(GuardianRelationship.Father),
                true,
                false)],
            confirmDuplicate);

    private sealed record SeededAccount(string UserName, string Password);

    private sealed record DuplicateWarningBody(string Code, IReadOnlyList<DuplicateMatchDto> Duplicates);

    private sealed class TestTenant : ICurrentTenant
    {
        public Guid? SchoolId { get; set; }

        public Guid? UserId { get; set; }

        public Guid? DeviceId { get; set; }
    }

    private sealed class StudentApiFactory : WebApplicationFactory<Program>, IAsyncDisposable
    {
        private readonly SqliteConnection _connection;
        private readonly string _connectionString;
        private readonly string _signingKey;
        private readonly Guid _schoolId = Guid.NewGuid();

        private StudentApiFactory()
        {
            var dataSource = $"asms-student-api-{Guid.NewGuid():N}";
            _connectionString = new SqliteConnectionStringBuilder
            {
                DataSource = dataSource,
                Mode = SqliteOpenMode.Memory,
                Cache = SqliteCacheMode.Shared
            }.ToString();
            _connection = new SqliteConnection(_connectionString);
            _connection.Open();
            _signingKey = Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
        }

        public static async Task<StudentApiFactory> CreateAsync()
        {
            var factory = new StudentApiFactory();
            await factory.MigrateAsync();
            return factory;
        }

        public async Task MigrateAsync()
        {
            var tenant = new TestTenant { SchoolId = _schoolId };
            var options = new DbContextOptionsBuilder<AsmsDbContext>()
                .UseSqlite(
                    _connection,
                    sqlite => sqlite.MigrationsAssembly("Asms.Infrastructure.Migrations.Sqlite"))
                .Options;
            await using var domain = new AsmsDbContext(options, tenant);
            await domain.Database.MigrateAsync();

            var identityOptions = new DbContextOptionsBuilder<AsmsIdentityDbContext>()
                .UseSqlite(
                    _connection,
                    sqlite => sqlite.MigrationsAssembly("Asms.Infrastructure.Migrations.Sqlite"))
                .Options;
            await using var identity = new AsmsIdentityDbContext(identityOptions);
            await identity.Database.MigrateAsync();

            domain.Schools.Add(new School
            {
                Id = _schoolId,
                Name = "API student school",
                Code = "DEMO1"
            });
            await domain.SaveChangesAsync();
            await new SchoolRoleSeeder(domain, tenant).SeedAsync(_schoolId);
        }

        public async Task<SeededAccount> CreateAccountAsync(string roleName)
        {
            using var scope = Services.CreateScope();
            var domain = scope.ServiceProvider.GetRequiredService<AsmsDbContext>();
            var identityManager = scope.ServiceProvider.GetRequiredService<UserManager<AsmsIdentityUser>>();
            var appUserId = Guid.NewGuid();
            var roleId = await domain.AppRoles
                .IgnoreQueryFilters()
                .Where(role => role.SchoolId == _schoolId)
                .Where(role => role.Name == roleName)
                .Select(role => role.Id)
                .SingleAsync();
            domain.AppUsers.Add(new AppUser
            {
                Id = appUserId,
                SchoolId = _schoolId,
                FullName = "API test account"
            });
            domain.UserRoles.Add(new UserRole
            {
                SchoolId = _schoolId,
                UserId = appUserId,
                RoleId = roleId
            });
            await domain.SaveChangesAsync();

            var userName = $"student-{Guid.NewGuid():N}";
            var password = $"Aa1{Guid.NewGuid():N}";
            var user = new AsmsIdentityUser
            {
                Id = Guid.NewGuid(),
                UserName = userName,
                SchoolId = _schoolId,
                AppUserId = appUserId,
                IsActive = true
            };
            var result = await identityManager.CreateAsync(user, password);
            Assert.True(result.Succeeded);
            return new SeededAccount(userName, password);
        }

        public async Task AuthenticateAsync(HttpClient client, SeededAccount account)
        {
            using var login = await client.PostAsJsonAsync(
                "/api/auth/login",
                new LoginRequest(account.UserName, account.Password));
            Assert.Equal(HttpStatusCode.OK, login.StatusCode);
            var response = await login.Content.ReadFromJsonAsync<LoginResponse>();
            Assert.NotNull(response);
            client.DefaultRequestHeaders.Authorization =
                new AuthenticationHeaderValue("Bearer", response.AccessToken);
        }

        public async Task<Guid> CreateOtherSchoolStudentAsync()
        {
            var schoolId = Guid.NewGuid();
            var tenant = new TestTenant { SchoolId = schoolId };
            var options = new DbContextOptionsBuilder<AsmsDbContext>()
                .UseSqlite(
                    _connection,
                    sqlite => sqlite.MigrationsAssembly("Asms.Infrastructure.Migrations.Sqlite"))
                .Options;
            await using var domain = new AsmsDbContext(options, tenant);
            domain.Schools.Add(new School
            {
                Id = schoolId,
                Name = "Other student school",
                Code = "OTHER1"
            });
            await domain.SaveChangesAsync();
            var student = new Student
            {
                SchoolId = schoolId,
                StudentNumber = "OTHER1-2026-0001",
                FirstName = "Isolated",
                LastName = "Student",
                FatherName = "Father",
                DateOfBirth = new DateOnly(2015, 2, 1),
                AdmissionDate = new DateOnly(2026, 3, 1)
            };
            student.RefreshDuplicateKey();
            domain.Students.Add(student);
            await domain.SaveChangesAsync();
            return student.Id;
        }

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment("Testing");
            builder.ConfigureAppConfiguration((_, configuration) =>
                configuration.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["Database:Provider"] = "Sqlite",
                    ["ConnectionStrings:Default"] = _connectionString,
                    ["Jwt:SigningKey"] = _signingKey
                }));
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
    }
}
