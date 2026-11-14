using System.Security.Cryptography;
using Asms.Infrastructure.Security;

namespace Asms.Infrastructure.Tests.Security;

public class JwtOptionsTests
{
    [Fact]
    public void Missing_signing_key_is_rejected_without_exposing_a_key()
    {
        var exception = Assert.Throws<InvalidOperationException>(
            () => new JwtOptions().Validate());

        Assert.DoesNotContain("SigningKey", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Short_signing_key_is_rejected_without_exposing_the_key()
    {
        var signingKey = new string('x', 8);
        var options = new JwtOptions { SigningKey = signingKey };

        var exception = Assert.Throws<InvalidOperationException>(options.Validate);

        Assert.DoesNotContain(signingKey, exception.Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(61)]
    public void Out_of_range_token_lifetime_is_rejected(int accessTokenMinutes)
    {
        var options = new JwtOptions
        {
            SigningKey = CreateSigningKey(),
            AccessTokenMinutes = accessTokenMinutes
        };

        Assert.Throws<InvalidOperationException>(options.Validate);
    }

    private static string CreateSigningKey() =>
        Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
}
