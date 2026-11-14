using System.Security.Cryptography;
using Asms.Application.Abstractions;
using Asms.Application.Auth;
using Asms.Domain.Security;
using Asms.Domain.Schools;
using Asms.Infrastructure.Identity;
using Asms.Infrastructure.Persistence;
using Asms.Infrastructure.Security;
using Asms.Infrastructure.Tests.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;

namespace Asms.Infrastructure.Tests.PostgreSql;

public class PostgreSqlRefreshTokenTests
{
    [PostgresFact]
    public async Task Refresh_token_migration_applies_and_model_has_no_pending_changes()
    {
        await using var database = await PostgresTestDatabase.CreateAsync();
        await using var context = CreateIdentityContext(database);

        await context.Database.MigrateAsync();

        var applied = context.Database.GetAppliedMigrations().ToArray();
        Assert.Equal(2, applied.Length);
        Assert.EndsWith("IdentityInitial", applied[0], StringComparison.Ordinal);
        Assert.EndsWith("AddRefreshTokens", applied[1], StringComparison.Ordinal);
        Assert.False(context.Database.HasPendingModelChanges());
        var tables = await context.Database
            .SqlQueryRaw<string>(
                "SELECT table_name AS \"Value\" FROM information_schema.tables WHERE table_schema = 'public'")
            .ToListAsync();
        Assert.Contains("Identity_RefreshTokens", tables);
    }

    [PostgresFact]
    public async Task Refresh_token_unique_and_foreign_key_constraints_are_enforced()
    {
        await using var database = await PostgresTestDatabase.CreateAsync();
        await using var context = CreateIdentityContext(database);
        await context.Database.MigrateAsync();
        var user = NewUser();
        context.Users.Add(user);
        await context.SaveChangesAsync();

        var duplicateHash = Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
        context.RefreshTokens.Add(NewRefreshToken(user.Id, duplicateHash));
        await context.SaveChangesAsync();
        context.RefreshTokens.Add(NewRefreshToken(user.Id, duplicateHash));
        var uniqueViolation = await Assert.ThrowsAsync<DbUpdateException>(
            () => context.SaveChangesAsync());
        Assert.Equal(
            PostgresErrorCodes.UniqueViolation,
            Assert.IsType<PostgresException>(uniqueViolation.InnerException).SqlState);

        context.ChangeTracker.Clear();
        context.RefreshTokens.Add(NewRefreshToken(
            Guid.NewGuid(),
            Convert.ToHexString(RandomNumberGenerator.GetBytes(32))));
        var foreignKeyViolation = await Assert.ThrowsAsync<DbUpdateException>(
            () => context.SaveChangesAsync());
        Assert.Equal(
            PostgresErrorCodes.ForeignKeyViolation,
            Assert.IsType<PostgresException>(foreignKeyViolation.InnerException).SqlState);
    }

    [PostgresFact]
    public async Task PostgreSql_refresh_rotates_a_token_in_the_same_family()
    {
        await using var database = await PostgresTestDatabase.CreateAsync();
        using var services = new PostgreSqlAuthServices(database);
        await services.MigrateAsync();
        var account = await services.CreateAccountAsync();
        var login = await services.LoginAsync(account);

        var refreshed = await services.RefreshAsync(login.RefreshToken!);

        Assert.True(refreshed.Succeeded);
        Assert.False(string.Equals(login.RefreshToken, refreshed.RefreshToken, StringComparison.Ordinal));
        var tokens = await services.GetRefreshTokensAsync(account.IdentityUserId);
        Assert.Equal(2, tokens.Count);
        Assert.Equal(tokens[0].FamilyId, tokens[1].FamilyId);
        Assert.Contains(true, tokens.Select(token => token.UsedAtUtc is not null));
    }

    [PostgresFact]
    public async Task PostgreSql_parallel_refresh_of_one_token_has_exactly_one_success()
    {
        await using var database = await PostgresTestDatabase.CreateAsync();
        using var services = new PostgreSqlAuthServices(database);
        await services.MigrateAsync();
        var account = await services.CreateAccountAsync();
        var login = await services.LoginAsync(account);

        var results = await Task.WhenAll(
            services.RefreshAsync(login.RefreshToken!),
            services.RefreshAsync(login.RefreshToken!));

        Assert.Equal(1, results.Count(result => result.Succeeded));
        Assert.Equal(1, results.Count(result => !result.Succeeded));
    }

    private static AsmsIdentityDbContext CreateIdentityContext(PostgresTestDatabase database)
    {
        var options = new DbContextOptionsBuilder<AsmsIdentityDbContext>();
        database.ConfigureIdentityDbContext(options);
        return new AsmsIdentityDbContext(options.Options);
    }

    private static AsmsIdentityUser NewUser()
    {
        var id = Guid.NewGuid().ToString("N");
        return new AsmsIdentityUser
        {
            Id = Guid.NewGuid(),
            UserName = $"user-{id}",
            NormalizedUserName = $"USER-{id}",
            SchoolId = Guid.NewGuid()
        };
    }

    private static RefreshToken NewRefreshToken(Guid userId, string tokenHash) =>
        new()
        {
            UserId = userId,
            SchoolId = Guid.NewGuid(),
            FamilyId = Guid.NewGuid(),
            TokenHash = tokenHash,
            SecurityStamp = "test-stamp",
            CreatedAtUtc = DateTime.UtcNow,
            ExpiresAtUtc = DateTime.UtcNow.AddDays(1),
            SessionExpiresAtUtc = DateTime.UtcNow.AddDays(2)
        };

    private sealed record TestAccount(
        Guid SchoolId,
        Guid IdentityUserId,
        Guid AppUserId,
        string UserName,
        string Password);

    private sealed class PostgreSqlAuthServices : IDisposable
    {
        private readonly PostgresTestDatabase _database;
        private readonly ServiceProvider _services;

        public PostgreSqlAuthServices(PostgresTestDatabase database)
        {
            _database = database;
            var services = new ServiceCollection();
            services.AddLogging();
            services.AddSingleton<ICurrentTenant>(new TestTenant());
            services.AddScoped<AsmsDbContext>(_ => _database.CreateContext(new TestTenant()));
            services.AddAsmsIdentity(_database.ConfigureIdentityDbContext);
            services.Configure<JwtOptions>(options =>
                options.SigningKey = Convert.ToHexString(RandomNumberGenerator.GetBytes(32)));
            services.AddSingleton<ITokenService, JwtTokenService>();
            services.AddSingleton(TimeProvider.System);
            services.AddScoped<IAuthService, AuthService>();
            _services = services.BuildServiceProvider();
        }

        public async Task MigrateAsync()
        {
            using var scope = _services.CreateScope();
            await scope.ServiceProvider.GetRequiredService<AsmsDbContext>()
                .Database.MigrateAsync();
            await scope.ServiceProvider.GetRequiredService<AsmsIdentityDbContext>()
                .Database.MigrateAsync();
        }

        public async Task<TestAccount> CreateAccountAsync()
        {
            var schoolId = Guid.NewGuid();
            var appUserId = Guid.NewGuid();
            var identityUserId = Guid.NewGuid();
            var userName = $"user-{Guid.NewGuid():N}";
            var password = $"Aa1{Guid.NewGuid():N}";
            using var scope = _services.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<AsmsDbContext>();
            context.Schools.Add(new School
            {
                Id = schoolId,
                Name = $"School-{schoolId:N}"
            });
            context.AppUsers.Add(new AppUser
            {
                Id = appUserId,
                SchoolId = schoolId,
                IdentityUserId = identityUserId,
                FullName = "Test account"
            });
            var roleId = Guid.NewGuid();
            context.AppRoles.Add(new AppRole
            {
                Id = roleId,
                SchoolId = schoolId,
                Name = "Teacher"
            });
            context.UserRoles.Add(new UserRole
            {
                SchoolId = schoolId,
                UserId = appUserId,
                RoleId = roleId
            });
            await context.SaveChangesAsync();

            var user = new AsmsIdentityUser
            {
                Id = identityUserId,
                UserName = userName,
                SchoolId = schoolId,
                AppUserId = appUserId
            };
            var result = await scope.ServiceProvider
                .GetRequiredService<UserManager<AsmsIdentityUser>>()
                .CreateAsync(user, password);
            Assert.True(result.Succeeded);
            return new TestAccount(schoolId, identityUserId, appUserId, userName, password);
        }

        public async Task<AuthResult> LoginAsync(TestAccount account)
        {
            using var scope = _services.CreateScope();
            return await scope.ServiceProvider.GetRequiredService<IAuthService>()
                .LoginAsync(account.UserName, account.Password);
        }

        public async Task<AuthResult> RefreshAsync(string token)
        {
            using var scope = _services.CreateScope();
            return await scope.ServiceProvider.GetRequiredService<IAuthService>()
                .RefreshAsync(token);
        }

        public async Task<List<RefreshToken>> GetRefreshTokensAsync(Guid userId)
        {
            using var scope = _services.CreateScope();
            return await scope.ServiceProvider.GetRequiredService<AsmsIdentityDbContext>()
                .RefreshTokens.AsNoTracking()
                .Where(token => token.UserId == userId)
                .OrderBy(token => token.CreatedAtUtc)
                .ToListAsync();
        }

        public void Dispose() => _services.Dispose();
    }
}
