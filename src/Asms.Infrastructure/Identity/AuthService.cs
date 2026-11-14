using System.Security.Cryptography;
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
    private readonly AsmsDbContext _db;
    private readonly ITokenService _tokenService;
    private readonly TimeProvider _timeProvider;

    public AuthService(
        UserManager<AsmsIdentityUser> userManager,
        IPasswordHasher<AsmsIdentityUser> passwordHasher,
        AsmsDbContext db,
        ITokenService tokenService,
        TimeProvider timeProvider)
    {
        _userManager = userManager;
        _passwordHasher = passwordHasher;
        _db = db;
        _tokenService = tokenService;
        _timeProvider = timeProvider;
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

        if (!user.IsActive ||
            wasLockedOut ||
            await _userManager.IsLockedOutAsync(user) ||
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
        return AuthResult.Success(token.Token, token.ExpiresAtUtc, user.Id, user.SchoolId, roles);
    }

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
