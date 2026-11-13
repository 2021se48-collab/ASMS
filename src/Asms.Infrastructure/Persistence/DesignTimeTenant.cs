using Asms.Application.Abstractions;

namespace Asms.Infrastructure.Persistence;

/// <summary>
/// Provides an empty tenant for design-time use.
/// </summary>
internal sealed class DesignTimeTenant : ICurrentTenant
{
    public Guid? SchoolId => null;

    public Guid? UserId => null;

    public Guid? DeviceId => null;
}
