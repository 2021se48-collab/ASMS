using Asms.Application.Abstractions;

namespace Asms.Infrastructure.Tests.Persistence;

public sealed class TestTenant : ICurrentTenant
{
    public Guid? SchoolId { get; set; }

    public Guid? UserId { get; set; }

    public Guid? DeviceId { get; set; }
}
