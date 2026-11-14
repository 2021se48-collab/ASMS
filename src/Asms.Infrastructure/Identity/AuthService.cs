using System.Security.Cryptography;
using System.Data.Common;
using System.Text;
using Asms.Application.Auth;
using Asms.Domain.Security;
using Asms.Infrastructure.Persistence;
using Asms.Infrastructure.Security;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace Asms.Infrastructure.Identity;

public sealed class AuthService : IAuthService
{
    private static readonly AsmsIdentityUser DummyUser = new();
    private static readonly string DummyPasswordHash = CreateDummyPasswordHash();

    private readonly UserManager<AsmsIdentityUser> _userManager;
    private readonly IPasswordHasher<AsmsIdentityUser> _passwordHasher;
    private readonly AsmsIdentityDbContext _identityDb;
    private readonly AsmsDbContext _db;
    private readonly ITokenService _tokenService;
    private readonly TimeProvider _timeProvider;
    private readonly JwtOptions _jwtOptions;

    public AuthService(
        UserManager<AsmsIdentityUser> userManager,
        IPasswordHasher<AsmsIdentityUser> passwordHasher,
        AsmsIdentityDbContext identityDb,
        AsmsDbContext db,
        ITokenService tokenService,
        TimeProvider timeProvider,
        Microsoft.Extensions.Options.IOptions<JwtOptions> jwtOptions)
    {
        _userManager = userManager;
        _passwordHasher = passwordHasher;
        _identityDb = identityDb;
        _db = db;
        _tokenService = tokenService;
        _timeProvider = timeProvider;
        _jwtOptions = jwtOptions.Value;
    }

    public async Task<AuthResult> LoginAsync(
        string userName,
        string password,
        CancellationToken ct = default)
    {
        var user = await _userManager.FindByNameAsync(userName);
        if (user is null)
        {
            _passwordHasher.VerifyHashedPassword(DummyUser, DummyPasswordHash, password);
            return AuthResult.Failed();
        }

        var wasLockedOut = await _userManager.IsLockedOutAsync(user);
        var passwordIsValid = await _userManager.CheckPasswordAsync(user, password);
        var passwordResult = passwordIsValid
            ? await _userManager.ResetAccessFailedCountAsync(user)
            : await _userManager.AccessFailedAsync(user);
        EnsureSucceeded(passwordResult, "update login failure state");

        var isLockedOut = await _userManager.IsLockedOutAsync(user);
        if (!wasLockedOut && isLockedOut)
        {
            await RevokeAllForUserAsync(user.Id, "LOCKOUT", ct);
        }

        if (!user.IsActive ||
            wasLockedOut ||
            isLockedOut ||
            !passwordIsValid)
        {
            return AuthResult.Failed();
        }

        if (user.SchoolId != Guid.Empty)
        {
            var school = await _db.Schools
                .IgnoreQueryFilters()
                .AsNoTracking()
                .SingleOrDefaultAsync(school => school.Id == user.SchoolId, ct);
            if (school is null || school.IsDeleted || !school.IsActive)
            {
                return AuthResult.Failed();
            }
        }

        var roles = await LoadRolesAsync(user, ct);
        if (roles.Count == 0)
        {
            return AuthResult.Failed();
        }

        var utcNow = _timeProvider.GetUtcNow().UtcDateTime;
        user.LastLoginAtUtc = utcNow;
        EnsureSucceeded(await _userManager.UpdateAsync(user), "update the login account");

        var token = _tokenService.CreateAccessToken(user.Id, user.SchoolId, roles, utcNow);
        var sessionExpiresAtUtc = utcNow.AddDays(_jwtOptions.SessionMaxDays);
        var (refreshToken, storedRefreshToken) = CreateRefreshToken(
            user,
            Guid.NewGuid(),
            utcNow,
            sessionExpiresAtUtc,
            Min(utcNow.AddDays(_jwtOptions.RefreshTokenDays), sessionExpiresAtUtc));
        _identityDb.RefreshTokens.Add(storedRefreshToken);
        await _identityDb.SaveChangesAsync(ct);

        return AuthResult.Success(
            token.Token,
            token.ExpiresAtUtc,
            user.Id,
            user.SchoolId,
            roles,
            refreshToken,
            storedRefreshToken.ExpiresAtUtc);
    }

    public async Task<AuthResult> RefreshAsync(
        string refreshToken,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(refreshToken))
        {
            return AuthResult.Failed();
        }

        var tokenHash = HashRefreshToken(refreshToken);
        try
        {
            await using var transaction = await _identityDb.Database.BeginTransactionAsync(ct);
            var storedToken = await _identityDb.RefreshTokens
                .AsNoTracking()
                .SingleOrDefaultAsync(token => token.TokenHash == tokenHash, ct);
            if (storedToken is null || storedToken.RevokedAtUtc is not null)
            {
                return AuthResult.Failed();
            }

            var now = _timeProvider.GetUtcNow().UtcDateTime;
            if (storedToken.UsedAtUtc is DateTime usedAtUtc)
            {
                if (now > usedAtUtc.AddSeconds(_jwtOptions.RefreshReuseGraceSeconds))
                {
                    await RevokeFamilyAsync(
                        storedToken.FamilyId,
                        now,
                        "REUSE_DETECTED",
                        ct);
                    await transaction.CommitAsync(ct);
                }

                return AuthResult.Failed();
            }

            if (storedToken.ExpiresAtUtc <= now ||
                storedToken.SessionExpiresAtUtc <= now)
            {
                return AuthResult.Failed();
            }

            var user = await _userManager.FindByIdAsync(storedToken.UserId.ToString());
            if (user is null ||
                !user.IsActive ||
                user.SchoolId != storedToken.SchoolId ||
                !StringComparer.Ordinal.Equals(user.SecurityStamp, storedToken.SecurityStamp) ||
                await _userManager.IsLockedOutAsync(user))
            {
                return AuthResult.Failed();
            }

            if (user.SchoolId != Guid.Empty)
            {
                var school = await _db.Schools
                    .IgnoreQueryFilters()
                    .AsNoTracking()
                    .SingleOrDefaultAsync(item => item.Id == user.SchoolId, ct);
                if (school is null || school.IsDeleted || !school.IsActive)
                {
                    return AuthResult.Failed();
                }
            }

            var roles = await LoadRolesAsync(user, ct);
            if (roles.Count == 0)
            {
                return AuthResult.Failed();
            }

            var sessionExpiresAtUtc = storedToken.SessionExpiresAtUtc;
            var expiresAtUtc = Min(now.AddDays(_jwtOptions.RefreshTokenDays), sessionExpiresAtUtc);
            var (newRefreshToken, newStoredToken) = CreateRefreshToken(
                user,
                storedToken.FamilyId,
                now,
                sessionExpiresAtUtc,
                expiresAtUtc);
            var accessToken = _tokenService.CreateAccessToken(user.Id, user.SchoolId, roles, now);

            var updated = await _identityDb.RefreshTokens
                .Where(token =>
                    token.Id == storedToken.Id &&
                    token.UsedAtUtc == null &&
                    token.RevokedAtUtc == null)
                .ExecuteUpdateAsync(
                    setters => setters
                        .SetProperty(token => token.UsedAtUtc, now)
                        .SetProperty(token => token.ReplacedByTokenId, newStoredToken.Id),
                    ct);
            if (updated == 0)
            {
                var latest = await _identityDb.RefreshTokens
                    .AsNoTracking()
                    .SingleOrDefaultAsync(token => token.Id == storedToken.Id, ct);
                if (latest?.UsedAtUtc is DateTime latestUsedAtUtc &&
                    now > latestUsedAtUtc.AddSeconds(_jwtOptions.RefreshReuseGraceSeconds))
                {
                    await RevokeFamilyAsync(
                        storedToken.FamilyId,
                        now,
                        "REUSE_DETECTED",
                        ct);
                    await transaction.CommitAsync(ct);
                }

                return AuthResult.Failed();
            }

            _identityDb.RefreshTokens.Add(newStoredToken);
            await _identityDb.SaveChangesAsync(ct);
            await transaction.CommitAsync(ct);

            return AuthResult.Success(
                accessToken.Token,
                accessToken.ExpiresAtUtc,
                user.Id,
                user.SchoolId,
                roles,
                newRefreshToken,
                newStoredToken.ExpiresAtUtc);
        }
        catch (DbException)
        {
            return AuthResult.Failed();
        }
    }

    public async Task LogoutAsync(
        string refreshToken,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(refreshToken))
        {
            return;
        }

        var tokenHash = HashRefreshToken(refreshToken);
        var storedToken = await _identityDb.RefreshTokens
            .AsNoTracking()
            .SingleOrDefaultAsync(token => token.TokenHash == tokenHash, ct);
        if (storedToken is null)
        {
            return;
        }

        await RevokeFamilyAsync(storedToken.FamilyId, _timeProvider.GetUtcNow().UtcDateTime, "LOGOUT", ct);
    }

    public async Task RevokeAllForUserAsync(
        Guid identityUserId,
        string reasonCode,
        CancellationToken ct = default)
    {
        var now = _timeProvider.GetUtcNow().UtcDateTime;
        await _identityDb.RefreshTokens
            .Where(token => token.UserId == identityUserId && token.RevokedAtUtc == null)
            .ExecuteUpdateAsync(
                setters => setters
                    .SetProperty(token => token.RevokedAtUtc, now)
                    .SetProperty(token => token.RevokedReason, reasonCode),
                ct);
    }

    private Task<int> RevokeFamilyAsync(
        Guid familyId,
        DateTime now,
        string reasonCode,
        CancellationToken ct) =>
        _identityDb.RefreshTokens
            .Where(token => token.FamilyId == familyId && token.RevokedAtUtc == null)
            .ExecuteUpdateAsync(
                setters => setters
                    .SetProperty(token => token.RevokedAtUtc, now)
                    .SetProperty(token => token.RevokedReason, reasonCode),
                ct);

    private static (string PlainText, RefreshToken Stored) CreateRefreshToken(
        AsmsIdentityUser user,
        Guid familyId,
        DateTime now,
        DateTime sessionExpiresAtUtc,
        DateTime expiresAtUtc)
    {
        var plainText = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32))
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');
        var stored = new RefreshToken
        {
            UserId = user.Id,
            SchoolId = user.SchoolId,
            FamilyId = familyId,
            TokenHash = HashRefreshToken(plainText),
            SecurityStamp = user.SecurityStamp ?? string.Empty,
            CreatedAtUtc = now,
            ExpiresAtUtc = expiresAtUtc,
            SessionExpiresAtUtc = sessionExpiresAtUtc
        };
        return (plainText, stored);
    }

    private static string HashRefreshToken(string refreshToken) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(refreshToken)));

    private static DateTime Min(DateTime first, DateTime second) =>
        first <= second ? first : second;

    private async Task<IReadOnlyList<string>> LoadRolesAsync(
        AsmsIdentityUser identityUser,
        CancellationToken ct)
    {
        if (identityUser.AppUserId is not Guid appUserId)
        {
            return Array.Empty<string>();
        }

        var appUserExists = await _db.AppUsers
            .IgnoreQueryFilters()
            .AsNoTracking()
            .AnyAsync(
                appUser => appUser.Id == appUserId &&
                    appUser.SchoolId == identityUser.SchoolId &&
                    !appUser.IsDeleted,
                ct);
        if (!appUserExists)
        {
            return Array.Empty<string>();
        }

        return await (
            from userRole in _db.UserRoles.IgnoreQueryFilters().AsNoTracking()
            join role in _db.AppRoles.IgnoreQueryFilters().AsNoTracking()
                on userRole.RoleId equals role.Id
            where userRole.UserId == appUserId &&
                userRole.SchoolId == identityUser.SchoolId &&
                !userRole.IsDeleted &&
                role.SchoolId == identityUser.SchoolId &&
                !role.IsDeleted
            select role.Name)
            .Distinct()
            .ToListAsync(ct);
    }

    private static string CreateDummyPasswordHash()
    {
        var randomPassword = Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
        return new PasswordHasher<AsmsIdentityUser>().HashPassword(DummyUser, randomPassword);
    }

    private static void EnsureSucceeded(IdentityResult result, string operation)
    {
        if (!result.Succeeded)
        {
            throw new InvalidOperationException($"Unable to {operation}.");
        }
    }
}
