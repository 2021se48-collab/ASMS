using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Asms.Application.Auth;
using Asms.Application.Security;
using Asms.Infrastructure.Security;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace Asms.Infrastructure.Tests.Security;

public class JwtTokenServiceTests
{
    [Fact]
    public async Task Token_contains_expected_claims_roles_and_expiry()
    {
        var options = CreateOptions();
        var service = new JwtTokenService(Options.Create(options));
        var now = DateTime.UtcNow.AddMinutes(-1);
        var userId = Guid.NewGuid();
        var schoolId = Guid.NewGuid();

        var token = service.CreateAccessToken(userId, schoolId, ["Principal", "Teacher"], now);
        var result = await Handler().ValidateTokenAsync(token.Token, Validation(options));

        Assert.True(result.IsValid);
        var identity = result.ClaimsIdentity!;
        Assert.Equal(userId.ToString(), identity.FindFirst("sub")?.Value);
        Assert.Equal(userId.ToString(), identity.FindFirst(AsmsClaimTypes.UserId)?.Value);
        Assert.Equal(schoolId.ToString(), identity.FindFirst(AsmsClaimTypes.SchoolId)?.Value);
        Assert.Equal(
            ["Principal", "Teacher"],
            identity.FindAll(AsmsClaimTypes.Role).Select(claim => claim.Value).ToArray());
        Assert.Equal(now.AddMinutes(options.AccessTokenMinutes), token.ExpiresAtUtc);
        Assert.Equal(
            new DateTimeOffset(token.ExpiresAtUtc).ToUnixTimeSeconds(),
            long.Parse(identity.FindFirst("exp")!.Value));
        Assert.NotEmpty(identity.FindFirst("jti")!.Value);
    }

    [Fact]
    public async Task Token_rejects_wrong_key_issuer_audience_expiry_and_algorithm()
    {
        var options = CreateOptions();
        var service = new JwtTokenService(Options.Create(options));
        var now = DateTime.UtcNow.AddMinutes(-1);
        var token = service.CreateAccessToken(Guid.NewGuid(), Guid.NewGuid(), [], now).Token;
        var handler = Handler();

        Assert.True((await handler.ValidateTokenAsync(token, Validation(options))).IsValid);
        var wrongKey = await handler.ValidateTokenAsync(
            token,
            Validation(options, signingKey: CreateSigningKey()));
        var wrongIssuer = await handler.ValidateTokenAsync(
            token,
            Validation(options, issuer: "wrong-issuer"));
        var wrongAudience = await handler.ValidateTokenAsync(
            token,
            Validation(options, audience: "wrong-audience"));
        Assert.False(wrongKey.IsValid);
        Assert.False(wrongIssuer.IsValid);
        Assert.False(wrongAudience.IsValid);

        var expiredToken = service.CreateAccessToken(
            Guid.NewGuid(),
            Guid.NewGuid(),
            [],
            now.AddHours(-2)).Token;
        Assert.False((await handler.ValidateTokenAsync(expiredToken, Validation(options))).IsValid);

        var alternateAlgorithmToken = CreateAlternateAlgorithmToken(options, now);
        var rejectedAlgorithm = await handler.ValidateTokenAsync(
            alternateAlgorithmToken,
            Validation(options));
        Assert.False(rejectedAlgorithm.IsValid);
    }

    [Fact]
    public async Task Each_token_has_a_different_jti()
    {
        var options = CreateOptions();
        var service = new JwtTokenService(Options.Create(options));
        var now = DateTime.UtcNow;
        var handler = Handler();

        var first = await handler.ValidateTokenAsync(
            service.CreateAccessToken(Guid.NewGuid(), Guid.NewGuid(), [], now).Token,
            Validation(options));
        var second = await handler.ValidateTokenAsync(
            service.CreateAccessToken(Guid.NewGuid(), Guid.NewGuid(), [], now).Token,
            Validation(options));

        Assert.True(first.IsValid);
        Assert.True(second.IsValid);
        Assert.NotEqual(
            first.ClaimsIdentity!.FindFirst("jti")!.Value,
            second.ClaimsIdentity!.FindFirst("jti")!.Value);
    }

    private static JwtOptions CreateOptions() => new()
    {
        SigningKey = CreateSigningKey()
    };

    private static string CreateSigningKey() =>
        Convert.ToHexString(RandomNumberGenerator.GetBytes(32));

    private static JsonWebTokenHandler Handler() => new()
    {
        SetDefaultTimesOnTokenCreation = false
    };

    private static TokenValidationParameters Validation(
        JwtOptions options,
        string? signingKey = null,
        string? issuer = null,
        string? audience = null) =>
        new()
        {
            ValidateIssuer = true,
            ValidIssuer = issuer ?? options.Issuer,
            ValidateAudience = true,
            ValidAudience = audience ?? options.Audience,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(
                Encoding.UTF8.GetBytes(signingKey ?? options.SigningKey)),
            ValidAlgorithms = [SecurityAlgorithms.HmacSha256],
            ClockSkew = TimeSpan.Zero
        };

    private static string CreateAlternateAlgorithmToken(JwtOptions options, DateTime now)
    {
        var descriptor = new SecurityTokenDescriptor
        {
            Issuer = options.Issuer,
            Audience = options.Audience,
            NotBefore = now,
            IssuedAt = now,
            Expires = now.AddMinutes(options.AccessTokenMinutes),
            Subject = new ClaimsIdentity([new Claim("sub", Guid.NewGuid().ToString())]),
            SigningCredentials = new SigningCredentials(
                new SymmetricSecurityKey(Encoding.UTF8.GetBytes(options.SigningKey)),
                SecurityAlgorithms.HmacSha384)
        };

        return Handler().CreateToken(descriptor);
    }
}
