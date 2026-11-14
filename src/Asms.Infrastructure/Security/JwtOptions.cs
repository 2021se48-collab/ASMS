namespace Asms.Infrastructure.Security;

public sealed class JwtOptions
{
    public const string SectionName = "Jwt";

    public string SigningKey { get; set; } = string.Empty;

    public string Issuer { get; set; } = "asms";

    public string Audience { get; set; } = "asms-clients";

    public int AccessTokenMinutes { get; set; } = 15;

    public int RefreshTokenDays { get; set; } = 30;

    public int SessionMaxDays { get; set; } = 90;

    public int RefreshReuseGraceSeconds { get; set; } = 30;

    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(SigningKey) || SigningKey.Length < 32)
        {
            throw new InvalidOperationException("JWT signing key must contain at least 32 characters.");
        }

        if (AccessTokenMinutes is < 1 or > 60)
        {
            throw new InvalidOperationException("JWT access-token lifetime must be between 1 and 60 minutes.");
        }

        if (RefreshTokenDays is < 1 or > 90)
        {
            throw new InvalidOperationException("Refresh-token lifetime must be between 1 and 90 days.");
        }

        if (SessionMaxDays < RefreshTokenDays || SessionMaxDays > 365)
        {
            throw new InvalidOperationException("Session lifetime must be at least the refresh-token lifetime and no more than 365 days.");
        }

        if (RefreshReuseGraceSeconds is < 0 or > 300)
        {
            throw new InvalidOperationException("Refresh-token reuse grace must be between 0 and 300 seconds.");
        }
    }
}
