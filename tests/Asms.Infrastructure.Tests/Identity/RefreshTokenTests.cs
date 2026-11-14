using System.Security.Cryptography;
using System.Text;
using Asms.Application.Abstractions;
using Asms.Application.Auth;
using Asms.Application.Security;
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
using Microsoft.IdentityModel.JsonWebTokens;

namespace Asms.Infrastructure.Tests.Identity;

public class RefreshTokenTests
{
    [Fact]
    public async Task Login_persists_only_the_SHA256_hash_of_the_refresh_token()
    {
        await using var database = await RefreshTestDatabase.CreateAsync();
        var account = await database.CreateAccountAsync();

        var result = await database.LoginAsync(account);
        var storedToken = await database.FindTokenAsync(result.RefreshToken!);
        var expectedHash = Convert.ToHexString(
            SHA256.HashData(Encoding.UTF8.GetBytes(result.RefreshToken!)));

        Assert.True(result.Succeeded);
        Assert.NotNull(result.RefreshToken);
        Assert.NotNull(storedToken);
        Assert.True(CryptographicOperations.FixedTimeEquals(
            Convert.FromHexString(expectedHash),
            Convert.FromHexString(storedToken.TokenHash)));
        Assert.False(storedToken.TokenHash.Contains(result.RefreshToken!, StringComparison.Ordinal));
        Assert.False(await database.TokenHashExistsAsync(result.RefreshToken!));
    }

    [Fact]
    public async Task Refresh_rotates_token_in_same_family_and_caps_expiry_at_session_limit()
    {
        await using var database = await RefreshTestDatabase.CreateAsync(
            refreshTokenDays: 30,
            sessionMaxDays: 31);
        var account = await database.CreateAccountAsync();
        var login = await database.LoginAsync(account);
        var oldToken = await database.FindTokenAsync(login.RefreshToken!);
        Assert.NotNull(oldToken);
        database.Clock.Advance(TimeSpan.FromDays(29));

        var refreshed = await database.RefreshAsync(login.RefreshToken!);
        var storedNewToken = await database.FindTokenAsync(refreshed.RefreshToken!);
        var storedOldToken = await database.FindTokenByIdAsync(oldToken.Id);

        Assert.True(refreshed.Succeeded);
        Assert.False(string.Equals(login.AccessToken, refreshed.AccessToken, StringComparison.Ordinal));
        Assert.False(string.Equals(login.RefreshToken, refreshed.RefreshToken, StringComparison.Ordinal));
        Assert.NotNull(storedNewToken);
        Assert.NotNull(storedOldToken?.UsedAtUtc);
        Assert.Equal(oldToken.FamilyId, storedNewToken.FamilyId);
        Assert.Equal(storedNewToken.Id, storedOldToken.ReplacedByTokenId);
        Assert.Equal(storedNewToken.SessionExpiresAtUtc, storedOldToken.SessionExpiresAtUtc);
        Assert.Equal(storedNewToken.SessionExpiresAtUtc, storedNewToken.ExpiresAtUtc);
        Assert.Equal(storedNewToken.ExpiresAtUtc, refreshed.RefreshTokenExpiresAtUtc);
    }

    [Fact]
    public async Task Reuse_within_grace_fails_without_revoking_the_newest_token()
    {
        await using var database = await RefreshTestDatabase.CreateAsync();
        var account = await database.CreateAccountAsync();
        var login = await database.LoginAsync(account);
        var rotation = await database.RefreshAsync(login.RefreshToken!);

        var reused = await database.RefreshAsync(login.RefreshToken!);
        var newest = await database.RefreshAsync(rotation.RefreshToken!);

        AssertFailed(reused);
        Assert.True(newest.Succeeded);
    }

    [Fact]
    public async Task Reuse_after_grace_revokes_the_whole_family()
    {
        await using var database = await RefreshTestDatabase.CreateAsync();
        var account = await database.CreateAccountAsync();
        var login = await database.LoginAsync(account);
        var rotation = await database.RefreshAsync(login.RefreshToken!);
        database.Clock.Advance(TimeSpan.FromSeconds(31));

        var reused = await database.RefreshAsync(login.RefreshToken!);
        var newest = await database.RefreshAsync(rotation.RefreshToken!);
        var family = await database.FindFamilyAsync(login.RefreshToken!);

        AssertFailed(reused);
        AssertFailed(newest);
        Assert.True(family.All(token =>
            token.RevokedAtUtc is not null &&
            token.RevokedReason == "REUSE_DETECTED"));
    }

    [Fact]
    public async Task Blank_unknown_and_malformed_tokens_return_the_same_failure()
    {
        await using var database = await RefreshTestDatabase.CreateAsync();
        var service = database.CreateAuthService();

        var results = new[]
        {
            await service.RefreshAsync(string.Empty),
            await service.RefreshAsync(" "),
            await service.RefreshAsync("not-a-refresh-token"),
            await service.RefreshAsync(Convert.ToBase64String(RandomNumberGenerator.GetBytes(32)))
        };

        foreach (var result in results)
        {
            AssertFailed(result);
        }
    }

    [Fact]
    public async Task Expired_token_and_expired_absolute_session_fail()
    {
        await using var database = await RefreshTestDatabase.CreateAsync();
        var account = await database.CreateAccountAsync();
        var expiredLogin = await database.LoginAsync(account);
        database.Clock.Advance(TimeSpan.FromDays(30));

        AssertFailed(await database.RefreshAsync(expiredLogin.RefreshToken!));

        await using var sessionDatabase = await RefreshTestDatabase.CreateAsync();
        var sessionAccount = await sessionDatabase.CreateAccountAsync();
        var sessionLogin = await sessionDatabase.LoginAsync(sessionAccount);
        await sessionDatabase.UpdateTokenAsync(sessionLogin.RefreshToken!, token =>
        {
            token.ExpiresAtUtc = sessionDatabase.Clock.GetUtcNow().UtcDateTime.AddDays(1);
            token.SessionExpiresAtUtc = sessionDatabase.Clock.GetUtcNow().UtcDateTime.AddTicks(-1);
        });

        AssertFailed(await sessionDatabase.RefreshAsync(sessionLogin.RefreshToken!));
    }

    [Fact]
    public async Task Deactivated_user_inactive_or_deleted_school_stamp_change_lockout_and_removed_roles_fail()
    {
        await using var database = await RefreshTestDatabase.CreateAsync();

        var inactiveUser = await database.CreateAccountAsync();
        var inactiveLogin = await database.LoginAsync(inactiveUser);
        await database.SetUserActiveAsync(inactiveUser.IdentityUserId, false);
        AssertFailed(await database.RefreshAsync(inactiveLogin.RefreshToken!));

        var inactiveSchool = await database.CreateAccountAsync();
        var inactiveSchoolLogin = await database.LoginAsync(inactiveSchool);
        await database.SetSchoolAsync(inactiveSchool.SchoolId, isActive: false);
        AssertFailed(await database.RefreshAsync(inactiveSchoolLogin.RefreshToken!));

        var deletedSchool = await database.CreateAccountAsync();
        var deletedSchoolLogin = await database.LoginAsync(deletedSchool);
        await database.SetSchoolAsync(deletedSchool.SchoolId, isDeleted: true);
        AssertFailed(await database.RefreshAsync(deletedSchoolLogin.RefreshToken!));

        var changedStamp = await database.CreateAccountAsync();
        var changedStampLogin = await database.LoginAsync(changedStamp);
        await database.UpdateSecurityStampAsync(changedStamp.IdentityUserId);
        AssertFailed(await database.RefreshAsync(changedStampLogin.RefreshToken!));

        var lockedOut = await database.CreateAccountAsync();
        var lockedOutLogin = await database.LoginAsync(lockedOut);
        for (var attempt = 0; attempt < 5; attempt++)
        {
            AssertFailed(await database.LoginAsync(lockedOut, $"wrong-{Guid.NewGuid():N}"));
        }

        AssertFailed(await database.RefreshAsync(lockedOutLogin.RefreshToken!));

        var noRoles = await database.CreateAccountAsync();
        var noRolesLogin = await database.LoginAsync(noRoles);
        await database.RemoveRolesAsync(noRoles.AppUserId);
        AssertFailed(await database.RefreshAsync(noRolesLogin.RefreshToken!));
    }

    [Fact]
    public async Task Refresh_uses_current_roles_and_rejects_a_changed_school()
    {
        await using var database = await RefreshTestDatabase.CreateAsync();
        var account = await database.CreateAccountAsync("Teacher");
        var login = await database.LoginAsync(account);
        await database.ReplaceRoleAsync(account, "Accountant");

        var refreshed = await database.RefreshAsync(login.RefreshToken!);
        var claims = new JsonWebToken(refreshed.AccessToken!).Claims;

        Assert.True(refreshed.Succeeded);
        Assert.Contains(claims, claim =>
            claim.Type == AsmsClaimTypes.Role && claim.Value == "Accountant");
        Assert.DoesNotContain(claims, claim =>
            claim.Type == AsmsClaimTypes.Role && claim.Value == "Teacher");

        var another = await database.CreateAccountAsync();
        var anotherLogin = await database.LoginAsync(another);
        await database.SetIdentitySchoolAsync(another.IdentityUserId, Guid.NewGuid());

        AssertFailed(await database.RefreshAsync(anotherLogin.RefreshToken!));
    }

    [Fact]
    public async Task Logout_and_revoke_all_revoke_only_the_target_families()
    {
        await using var database = await RefreshTestDatabase.CreateAsync();
        var account = await database.CreateAccountAsync();
        var firstLogin = await database.LoginAsync(account);
        await database.LogoutAsync(firstLogin.RefreshToken!);
        await database.LogoutAsync("unknown-token");

        var loggedOutFamily = await database.FindFamilyAsync(firstLogin.RefreshToken!);
        var loggedOutFamilyId = Assert.Single(loggedOutFamily.Select(token => token.FamilyId));
        Assert.True(loggedOutFamily.All(token =>
            token.RevokedAtUtc is not null &&
            token.RevokedReason == "LOGOUT"));

        var secondLogin = await database.LoginAsync(account);
        var thirdLogin = await database.LoginAsync(account);
        var otherUser = await database.CreateAccountAsync();
        var otherLogin = await database.LoginAsync(otherUser);

        await database.RevokeAllAsync(account.IdentityUserId, "LOGOUT_ALL");

        var userTokens = await database.FindUserTokensAsync(account.IdentityUserId);
        var otherTokens = await database.FindUserTokensAsync(otherUser.IdentityUserId);
        Assert.True(userTokens.All(token =>
            token.RevokedAtUtc is not null &&
            token.RevokedReason ==
                (token.FamilyId == loggedOutFamilyId ? "LOGOUT" : "LOGOUT_ALL")));
        Assert.True(otherTokens.All(token => token.RevokedAtUtc is null));
        AssertFailed(await database.RefreshAsync(secondLogin.RefreshToken!));
        AssertFailed(await database.RefreshAsync(thirdLogin.RefreshToken!));
        Assert.True((await database.RefreshAsync(otherLogin.RefreshToken!)).Succeeded);
    }

    [Fact]
    public async Task Lockout_start_revokes_existing_refresh_tokens()
    {
        await using var database = await RefreshTestDatabase.CreateAsync();
        var account = await database.CreateAccountAsync();
        var login = await database.LoginAsync(account);

        for (var attempt = 0; attempt < 5; attempt++)
        {
            AssertFailed(await database.LoginAsync(account, $"wrong-{Guid.NewGuid():N}"));
        }

        var stored = await database.FindTokenAsync(login.RefreshToken!);
        Assert.NotNull(stored?.RevokedAtUtc);
        Assert.Equal("LOCKOUT", stored.RevokedReason);
        AssertFailed(await database.RefreshAsync(login.RefreshToken!));
    }

    [Fact]
    public async Task Parallel_refresh_of_one_token_has_exactly_one_success()
    {
        await using var database = await RefreshTestDatabase.CreateAsync();
        var account = await database.CreateAccountAsync();
        var login = await database.LoginAsync(account);

        using var first = database.CreateAuthService();
        using var second = database.CreateAuthService();
        var results = await Task.WhenAll(
            first.RefreshAsync(login.RefreshToken!),
            second.RefreshAsync(login.RefreshToken!));

        Assert.Equal(1, results.Count(result => result.Succeeded));
        Assert.Equal(1, results.Count(result => !result.Succeeded));
    }

    [Fact]
    public async Task Cleaner_deletes_only_tokens_expired_longer_than_keep_for()
    {
        await using var database = await RefreshTestDatabase.CreateAsync();
        var account = await database.CreateAccountAsync();
        var now = database.Clock.GetUtcNow().UtcDateTime;
        var old = new RefreshToken
        {
            UserId = account.IdentityUserId,
            SchoolId = account.SchoolId,
            FamilyId = Guid.NewGuid(),
            TokenHash = HashForTest("old"),
            SecurityStamp = account.SecurityStamp,
            CreatedAtUtc = now.AddDays(-10),
            ExpiresAtUtc = now.AddDays(-3),
            SessionExpiresAtUtc = now.AddDays(-3)
        };
        var recentlyExpired = new RefreshToken
        {
            UserId = account.IdentityUserId,
            SchoolId = account.SchoolId,
            FamilyId = Guid.NewGuid(),
            TokenHash = HashForTest("recent"),
            SecurityStamp = account.SecurityStamp,
            CreatedAtUtc = now.AddDays(-2),
            ExpiresAtUtc = now.AddHours(-12),
            SessionExpiresAtUtc = now.AddHours(-12)
        };
        var activeSession = new RefreshToken
        {
            UserId = account.IdentityUserId,
            SchoolId = account.SchoolId,
            FamilyId = Guid.NewGuid(),
            TokenHash = HashForTest("active"),
            SecurityStamp = account.SecurityStamp,
            CreatedAtUtc = now.AddDays(-2),
            ExpiresAtUtc = now.AddDays(-2),
            SessionExpiresAtUtc = now.AddDays(2)
        };
        await database.AddTokensAsync(old, recentlyExpired, activeSession);
        var deleted = await database.PurgeExpiredAsync(TimeSpan.FromDays(1));
        var remaining = await database.FindUserTokensAsync(account.IdentityUserId);

        Assert.Equal(1, deleted);
        Assert.DoesNotContain(old.Id, remaining.Select(token => token.Id));
        Assert.Contains(recentlyExpired.Id, remaining.Select(token => token.Id));
        Assert.Contains(activeSession.Id, remaining.Select(token => token.Id));
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(
            () => database.PurgeExpiredAsync(TimeSpan.FromSeconds(-1)));
    }

    [Theory]
    [InlineData(0, 90, 30)]
    [InlineData(91, 91, 30)]
    [InlineData(30, 29, 30)]
    [InlineData(30, 366, 30)]
    [InlineData(30, 90, -1)]
    [InlineData(30, 90, 301)]
    public void Invalid_refresh_options_are_rejected_without_exposing_secrets(
        int refreshDays,
        int sessionDays,
        int graceSeconds)
    {
        var signingKey = Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
        var options = new JwtOptions
        {
            SigningKey = signingKey,
            RefreshTokenDays = refreshDays,
            SessionMaxDays = sessionDays,
            RefreshReuseGraceSeconds = graceSeconds
        };

        var exception = Assert.Throws<InvalidOperationException>(options.Validate);

        Assert.False(exception.Message.Contains(signingKey, StringComparison.Ordinal));
    }

    private static void AssertFailed(AuthResult result)
    {
        Assert.False(result.Succeeded);
        Assert.Null(result.AccessToken);
        Assert.Null(result.ExpiresAtUtc);
        Assert.Null(result.UserId);
        Assert.Null(result.SchoolId);
        Assert.Empty(result.Roles);
        Assert.Null(result.RefreshToken);
        Assert.Null(result.RefreshTokenExpiresAtUtc);
    }

    private static string HashForTest(string value) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value)));

    private sealed record TestAccount(
        Guid SchoolId,
        Guid IdentityUserId,
        Guid AppUserId,
        string UserName,
        string Password,
        string SecurityStamp);

    private sealed class RefreshTestDatabase : IAsyncDisposable
    {
        private readonly SqliteConnection _anchor;
        private readonly string _connectionString;
        private readonly ServiceProvider _services;

        private RefreshTestDatabase(
            int refreshTokenDays,
            int sessionMaxDays,
            int refreshReuseGraceSeconds)
        {
            var name = $"asms-refresh-{Guid.NewGuid():N}";
            _connectionString = new SqliteConnectionStringBuilder
            {
                DataSource = name,
                Mode = SqliteOpenMode.Memory,
                Cache = SqliteCacheMode.Shared
            }.ToString();
            _anchor = new SqliteConnection(_connectionString);
            _anchor.Open();
            Clock = new FakeTimeProvider(new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero));

            var services = new ServiceCollection();
            services.AddLogging();
            services.AddSingleton<ICurrentTenant>(new TestTenant());
            services.AddDbContext<AsmsDbContext>(options =>
                options.UseSqlite(
                    _connectionString,
                    sqlite => sqlite.MigrationsAssembly("Asms.Infrastructure.Migrations.Sqlite")));
            services.AddAsmsIdentity(options =>
                options.UseSqlite(
                    _connectionString,
                    sqlite => sqlite.MigrationsAssembly("Asms.Infrastructure.Migrations.Sqlite")));
            services.Configure<JwtOptions>(options =>
            {
                options.SigningKey = Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
                options.RefreshTokenDays = refreshTokenDays;
                options.SessionMaxDays = sessionMaxDays;
                options.RefreshReuseGraceSeconds = refreshReuseGraceSeconds;
            });
            services.AddSingleton<TimeProvider>(Clock);
            services.AddSingleton<ITokenService, JwtTokenService>();
            services.AddScoped<IAuthService, AuthService>();
            _services = services.BuildServiceProvider();
        }

        public FakeTimeProvider Clock { get; }

        public static async Task<RefreshTestDatabase> CreateAsync(
            int refreshTokenDays = 30,
            int sessionMaxDays = 90,
            int refreshReuseGraceSeconds = 30)
        {
            var database = new RefreshTestDatabase(
                refreshTokenDays,
                sessionMaxDays,
                refreshReuseGraceSeconds);
            using var scope = database._services.CreateScope();
            await scope.ServiceProvider.GetRequiredService<AsmsDbContext>()
                .Database.MigrateAsync();
            await scope.ServiceProvider.GetRequiredService<AsmsIdentityDbContext>()
                .Database.MigrateAsync();
            return database;
        }

        public async Task<TestAccount> CreateAccountAsync(string roleName = "Teacher")
        {
            var schoolId = Guid.NewGuid();
            var appUserId = Guid.NewGuid();
            var userName = $"user-{Guid.NewGuid():N}";
            var password = $"Aa1{Guid.NewGuid():N}";
            var identityUserId = Guid.NewGuid();
            using (var scope = _services.CreateScope())
            {
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
                    FullName = "Test account",
                    IdentityUserId = identityUserId
                });
                var roleId = Guid.NewGuid();
                context.AppRoles.Add(new AppRole
                {
                    Id = roleId,
                    SchoolId = schoolId,
                    Name = roleName
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
                var createResult = await scope.ServiceProvider
                    .GetRequiredService<UserManager<AsmsIdentityUser>>()
                    .CreateAsync(user, password);
                Assert.True(createResult.Succeeded);
                return new TestAccount(
                    schoolId,
                    identityUserId,
                    appUserId,
                    userName,
                    password,
                    user.SecurityStamp!);
            }
        }

        public async Task<AuthResult> LoginAsync(TestAccount account, string? password = null)
        {
            using var service = CreateAuthService();
            return await service.LoginAsync(account.UserName, password ?? account.Password);
        }

        public async Task<AuthResult> RefreshAsync(string token)
        {
            using var service = CreateAuthService();
            return await service.RefreshAsync(token);
        }

        public async Task LogoutAsync(string token)
        {
            using var service = CreateAuthService();
            await service.LogoutAsync(token);
        }

        public async Task RevokeAllAsync(Guid userId, string reason)
        {
            using var service = CreateAuthService();
            await service.RevokeAllForUserAsync(userId, reason);
        }

        public async Task<int> PurgeExpiredAsync(TimeSpan keepFor)
        {
            using var scope = _services.CreateScope();
            var cleaner = new RefreshTokenCleaner(
                scope.ServiceProvider.GetRequiredService<AsmsIdentityDbContext>(),
                Clock);
            return await cleaner.PurgeExpiredAsync(keepFor);
        }

        public ScopedAuthService CreateAuthService()
        {
            var scope = _services.CreateScope();
            return new ScopedAuthService(scope);
        }

        public async Task<RefreshToken?> FindTokenAsync(string plainText)
        {
            using var scope = _services.CreateScope();
            var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(plainText)));
            return await scope.ServiceProvider.GetRequiredService<AsmsIdentityDbContext>()
                .RefreshTokens.AsNoTracking().SingleOrDefaultAsync(token => token.TokenHash == hash);
        }

        public async Task<RefreshToken?> FindTokenByIdAsync(Guid id)
        {
            using var scope = _services.CreateScope();
            return await scope.ServiceProvider.GetRequiredService<AsmsIdentityDbContext>()
                .RefreshTokens.AsNoTracking().SingleOrDefaultAsync(token => token.Id == id);
        }

        public async Task<List<RefreshToken>> FindFamilyAsync(string plainText)
        {
            var token = await FindTokenAsync(plainText);
            Assert.NotNull(token);
            using var scope = _services.CreateScope();
            return await scope.ServiceProvider.GetRequiredService<AsmsIdentityDbContext>()
                .RefreshTokens.AsNoTracking()
                .Where(item => item.FamilyId == token.FamilyId)
                .ToListAsync();
        }

        public async Task<List<RefreshToken>> FindUserTokensAsync(Guid userId)
        {
            using var scope = _services.CreateScope();
            return await scope.ServiceProvider.GetRequiredService<AsmsIdentityDbContext>()
                .RefreshTokens.AsNoTracking()
                .Where(token => token.UserId == userId)
                .ToListAsync();
        }

        public async Task<bool> TokenHashExistsAsync(string value)
        {
            using var scope = _services.CreateScope();
            return await scope.ServiceProvider.GetRequiredService<AsmsIdentityDbContext>()
                .RefreshTokens.AnyAsync(token => token.TokenHash == value);
        }

        public async Task UpdateTokenAsync(string plainText, Action<RefreshToken> update)
        {
            using var scope = _services.CreateScope();
            var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(plainText)));
            var context = scope.ServiceProvider.GetRequiredService<AsmsIdentityDbContext>();
            var token = await context.RefreshTokens.SingleAsync(item => item.TokenHash == hash);
            update(token);
            await context.SaveChangesAsync();
        }

        public async Task SetUserActiveAsync(Guid userId, bool isActive)
        {
            using var scope = _services.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<AsmsIdentityDbContext>();
            var user = await context.Users.SingleAsync(item => item.Id == userId);
            user.IsActive = isActive;
            await context.SaveChangesAsync();
        }

        public async Task SetIdentitySchoolAsync(Guid userId, Guid schoolId)
        {
            using var scope = _services.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<AsmsIdentityDbContext>();
            var user = await context.Users.SingleAsync(item => item.Id == userId);
            user.SchoolId = schoolId;
            await context.SaveChangesAsync();
        }

        public async Task SetSchoolAsync(
            Guid schoolId,
            bool? isActive = null,
            bool? isDeleted = null)
        {
            using var scope = _services.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<AsmsDbContext>();
            var school = await context.Schools.IgnoreQueryFilters()
                .SingleAsync(item => item.Id == schoolId);
            if (isActive.HasValue)
            {
                school.IsActive = isActive.Value;
            }

            if (isDeleted.HasValue)
            {
                school.IsDeleted = isDeleted.Value;
            }

            await context.SaveChangesAsync();
        }

        public async Task UpdateSecurityStampAsync(Guid userId)
        {
            using var scope = _services.CreateScope();
            var users = scope.ServiceProvider.GetRequiredService<UserManager<AsmsIdentityUser>>();
            var user = await users.FindByIdAsync(userId.ToString());
            Assert.NotNull(user);
            Assert.True((await users.UpdateSecurityStampAsync(user)).Succeeded);
        }

        public async Task RemoveRolesAsync(Guid appUserId)
        {
            using var scope = _services.CreateScope();
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

        public async Task ReplaceRoleAsync(TestAccount account, string newRoleName)
        {
            using var scope = _services.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<AsmsDbContext>();
            var existing = await context.UserRoles.IgnoreQueryFilters()
                .SingleAsync(item => item.UserId == account.AppUserId);
            existing.IsDeleted = true;
            context.AppRoles.Add(new AppRole
            {
                SchoolId = account.SchoolId,
                Name = newRoleName
            });
            await context.SaveChangesAsync();
            var newRole = await context.AppRoles.IgnoreQueryFilters()
                .SingleAsync(item =>
                    item.SchoolId == account.SchoolId &&
                    item.Name == newRoleName);
            context.UserRoles.Add(new UserRole
            {
                SchoolId = account.SchoolId,
                UserId = account.AppUserId,
                RoleId = newRole.Id
            });
            await context.SaveChangesAsync();
        }

        public async Task AddTokensAsync(params RefreshToken[] tokens)
        {
            using var scope = _services.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<AsmsIdentityDbContext>();
            context.RefreshTokens.AddRange(tokens);
            await context.SaveChangesAsync();
        }

        public async ValueTask DisposeAsync()
        {
            await _services.DisposeAsync();
            await _anchor.DisposeAsync();
        }

    }

    private sealed class ScopedAuthService : IAuthService, IDisposable
    {
        private readonly IServiceScope _scope;
        private readonly IAuthService _service;

        public ScopedAuthService(IServiceScope scope)
        {
            _scope = scope;
            _service = scope.ServiceProvider.GetRequiredService<IAuthService>();
        }

        public Task<AuthResult> LoginAsync(string userName, string password, CancellationToken ct = default) =>
            _service.LoginAsync(userName, password, ct);

        public Task<AuthResult> RefreshAsync(string refreshToken, CancellationToken ct = default) =>
            _service.RefreshAsync(refreshToken, ct);

        public Task LogoutAsync(string refreshToken, CancellationToken ct = default) =>
            _service.LogoutAsync(refreshToken, ct);

        public Task RevokeAllForUserAsync(Guid identityUserId, string reasonCode, CancellationToken ct = default) =>
            _service.RevokeAllForUserAsync(identityUserId, reasonCode, ct);

        public void Dispose() => _scope.Dispose();
    }

    private sealed class FakeTimeProvider : TimeProvider
    {
        private DateTimeOffset _utcNow;

        public FakeTimeProvider(DateTimeOffset utcNow)
        {
            _utcNow = utcNow;
        }

        public override DateTimeOffset GetUtcNow() => _utcNow;

        public void Advance(TimeSpan amount) => _utcNow = _utcNow.Add(amount);
    }
}
