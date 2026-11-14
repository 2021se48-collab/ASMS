using Asms.Application.Abstractions;
using Asms.Application.Security;

namespace Asms.Api.Tenancy;

/// <summary>
/// Unknown means null, and null means the database returns nothing (safe default).
/// </summary>
public class HttpContextCurrentTenant : ICurrentTenant
{
    private readonly IHttpContextAccessor _accessor;

    public HttpContextCurrentTenant(IHttpContextAccessor accessor)
    {
        _accessor = accessor;
    }

    public Guid? SchoolId => GetClaimId(AsmsClaimTypes.SchoolId);

    public Guid? UserId => GetClaimId(AsmsClaimTypes.UserId);

    public Guid? DeviceId => GetClaimId(AsmsClaimTypes.DeviceId);

    private Guid? GetClaimId(string claimType)
    {
        var user = _accessor.HttpContext?.User;
        if (user?.Identity?.IsAuthenticated != true)
        {
            return null;
        }

        var claim = user.FindFirst(claimType);
        if (claim is null ||
            !Guid.TryParse(claim.Value, out var id) ||
            id == Guid.Empty)
        {
            return null;
        }

        return id;
    }
}
