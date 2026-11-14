using System.Security.Claims;
using System.Text;
using Asms.Application.Auth;
using Asms.Application.Security;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace Asms.Infrastructure.Security;

public sealed class JwtTokenService : ITokenService
{
    private readonly JwtOptions _options;

    public JwtTokenService(IOptions<JwtOptions> options)
    {
        _options = options.Value;
        _options.Validate();
    }

    public (string Token, DateTime ExpiresAtUtc) CreateAccessToken(
        Guid userId,
        Guid schoolId,
        IReadOnlyList<string> roles,
        DateTime utcNow)
    {
        ArgumentNullException.ThrowIfNull(roles);
        if (utcNow.Kind != DateTimeKind.Utc)
        {
            throw new ArgumentException("The issue time must be UTC.", nameof(utcNow));
        }

        var expiresAtUtc = utcNow.AddMinutes(_options.AccessTokenMinutes);
        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, userId.ToString()),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
            new(AsmsClaimTypes.UserId, userId.ToString()),
            new(AsmsClaimTypes.SchoolId, schoolId.ToString())
        };
        claims.AddRange(roles.Select(role => new Claim(AsmsClaimTypes.Role, role)));

        var descriptor = new SecurityTokenDescriptor
        {
            Issuer = _options.Issuer,
            Audience = _options.Audience,
            NotBefore = utcNow,
            IssuedAt = utcNow,
            Expires = expiresAtUtc,
            Subject = new ClaimsIdentity(claims),
            SigningCredentials = new SigningCredentials(
                new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_options.SigningKey)),
                SecurityAlgorithms.HmacSha256)
        };

        var token = new JsonWebTokenHandler
        {
            SetDefaultTimesOnTokenCreation = false
        }.CreateToken(descriptor);

        return (token, expiresAtUtc);
    }
}
