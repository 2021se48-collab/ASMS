using Asms.Application.Security;

namespace Asms.Api.Tests.Security;

public class AsmsClaimTypesTests
{
    [Fact]
    public void Claim_types_are_distinct_non_empty_and_use_the_asms_prefix()
    {
        var claimTypes = new[]
        {
            AsmsClaimTypes.SchoolId,
            AsmsClaimTypes.UserId,
            AsmsClaimTypes.DeviceId,
            AsmsClaimTypes.Role
        };

        Assert.Equal(claimTypes.Length, claimTypes.Distinct(StringComparer.Ordinal).Count());
        Assert.All(claimTypes, claimType =>
        {
            Assert.False(string.IsNullOrWhiteSpace(claimType));
            Assert.StartsWith("asms:", claimType, StringComparison.Ordinal);
        });
        Assert.Equal("asms:school_id", AsmsClaimTypes.SchoolId);
        Assert.Equal("asms:user_id", AsmsClaimTypes.UserId);
        Assert.Equal("asms:device_id", AsmsClaimTypes.DeviceId);
        Assert.Equal("asms:role", AsmsClaimTypes.Role);
    }
}
