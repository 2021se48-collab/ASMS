using System.Security.Claims;
using Asms.Api.Tenancy;
using Asms.Application.Security;
using Microsoft.AspNetCore.Http;

namespace Asms.Api.Tests.Tenancy;

public class HttpContextCurrentTenantTests
{
    [Fact]
    public void Authenticated_claims_return_school_user_and_device_ids()
    {
        var schoolId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var deviceId = Guid.NewGuid();
        var tenant = CreateTenant(
            new Claim(AsmsClaimTypes.SchoolId, schoolId.ToString()),
            new Claim(AsmsClaimTypes.UserId, userId.ToString()),
            new Claim(AsmsClaimTypes.DeviceId, deviceId.ToString()));

        Assert.Equal(schoolId, tenant.SchoolId);
        Assert.Equal(userId, tenant.UserId);
        Assert.Equal(deviceId, tenant.DeviceId);
    }

    [Fact]
    public void Missing_http_context_returns_null_for_all_ids()
    {
        var tenant = new HttpContextCurrentTenant(new HttpContextAccessor());

        Assert.Null(tenant.SchoolId);
        Assert.Null(tenant.UserId);
        Assert.Null(tenant.DeviceId);
    }

    [Fact]
    public void Unauthenticated_claims_return_null()
    {
        var context = new DefaultHttpContext
        {
            User = new ClaimsPrincipal(new ClaimsIdentity(new[]
            {
                new Claim(AsmsClaimTypes.SchoolId, Guid.NewGuid().ToString()),
                new Claim(AsmsClaimTypes.UserId, Guid.NewGuid().ToString()),
                new Claim(AsmsClaimTypes.DeviceId, Guid.NewGuid().ToString())
            }))
        };
        var tenant = new HttpContextCurrentTenant(new HttpContextAccessor { HttpContext = context });

        Assert.Null(tenant.SchoolId);
        Assert.Null(tenant.UserId);
        Assert.Null(tenant.DeviceId);
    }

    [Fact]
    public void Missing_claims_return_null()
    {
        var tenant = CreateTenant(new Claim(AsmsClaimTypes.SchoolId, Guid.NewGuid().ToString()));

        Assert.NotNull(tenant.SchoolId);
        Assert.Null(tenant.UserId);
        Assert.Null(tenant.DeviceId);
    }

    [Fact]
    public void Invalid_claim_text_returns_null()
    {
        var tenant = CreateTenant(
            new Claim(AsmsClaimTypes.SchoolId, "not-a-guid"),
            new Claim(AsmsClaimTypes.UserId, "invalid"),
            new Claim(AsmsClaimTypes.DeviceId, "invalid"));

        Assert.Null(tenant.SchoolId);
        Assert.Null(tenant.UserId);
        Assert.Null(tenant.DeviceId);
    }

    [Fact]
    public void Empty_guid_claims_return_null()
    {
        var tenant = CreateTenant(
            new Claim(AsmsClaimTypes.SchoolId, Guid.Empty.ToString()),
            new Claim(AsmsClaimTypes.UserId, Guid.Empty.ToString()),
            new Claim(AsmsClaimTypes.DeviceId, Guid.Empty.ToString()));

        Assert.Null(tenant.SchoolId);
        Assert.Null(tenant.UserId);
        Assert.Null(tenant.DeviceId);
    }

    [Fact]
    public void Duplicate_claims_use_the_first_value()
    {
        var firstSchoolId = Guid.NewGuid();
        var firstUserId = Guid.NewGuid();
        var firstDeviceId = Guid.NewGuid();
        var tenant = CreateTenant(
            new Claim(AsmsClaimTypes.SchoolId, firstSchoolId.ToString()),
            new Claim(AsmsClaimTypes.SchoolId, Guid.NewGuid().ToString()),
            new Claim(AsmsClaimTypes.UserId, firstUserId.ToString()),
            new Claim(AsmsClaimTypes.UserId, Guid.NewGuid().ToString()),
            new Claim(AsmsClaimTypes.DeviceId, firstDeviceId.ToString()),
            new Claim(AsmsClaimTypes.DeviceId, Guid.NewGuid().ToString()));

        Assert.Equal(firstSchoolId, tenant.SchoolId);
        Assert.Equal(firstUserId, tenant.UserId);
        Assert.Equal(firstDeviceId, tenant.DeviceId);
    }

    [Fact]
    public void Claims_from_one_request_do_not_leak_into_another_instance()
    {
        var firstSchoolId = Guid.NewGuid();
        var secondSchoolId = Guid.NewGuid();
        var accessor = new HttpContextAccessor
        {
            HttpContext = CreateContext(new Claim(AsmsClaimTypes.SchoolId, firstSchoolId.ToString()))
        };
        var firstTenant = new HttpContextCurrentTenant(accessor);
        Assert.Equal(firstSchoolId, firstTenant.SchoolId);

        accessor.HttpContext = CreateContext(new Claim(AsmsClaimTypes.SchoolId, secondSchoolId.ToString()));
        var secondTenant = new HttpContextCurrentTenant(accessor);

        Assert.Equal(secondSchoolId, secondTenant.SchoolId);
        Assert.NotEqual(firstSchoolId, secondTenant.SchoolId);
    }

    private static HttpContextCurrentTenant CreateTenant(params Claim[] claims)
    {
        return new HttpContextCurrentTenant(new HttpContextAccessor { HttpContext = CreateContext(claims) });
    }

    private static DefaultHttpContext CreateContext(params Claim[] claims)
    {
        var identity = new ClaimsIdentity(claims, "test");
        return new DefaultHttpContext { User = new ClaimsPrincipal(identity) };
    }
}
