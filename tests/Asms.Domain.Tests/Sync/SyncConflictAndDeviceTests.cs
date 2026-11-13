using Asms.Domain.Common;
using Asms.Domain.Sync;

namespace Asms.Domain.Tests.Sync;

public class SyncConflictAndDeviceTests
{
    [Fact]
    public void Sync_conflict_and_device_implement_ITenantEntity()
    {
        Assert.IsAssignableFrom<ITenantEntity>(new SyncConflict());
        Assert.IsAssignableFrom<ITenantEntity>(new SyncDevice());
    }

    [Fact]
    public void Resolve_sets_resolution_fields()
    {
        var conflict = new SyncConflict();
        var userId = Guid.NewGuid();
        var resolvedAtUtc = new DateTime(2026, 11, 13, 9, 0, 0, DateTimeKind.Utc);

        conflict.Resolve("Keep local data", userId, resolvedAtUtc);

        Assert.True(conflict.IsResolved);
        Assert.Equal("Keep local data", conflict.Resolution);
        Assert.Equal(userId, conflict.ResolvedByUserId);
        Assert.Equal(resolvedAtUtc, conflict.ResolvedAtUtc);
    }

    [Fact]
    public void Resolve_throws_for_blank_resolution()
    {
        Assert.Throws<ArgumentException>(() =>
            new SyncConflict().Resolve(" ", Guid.NewGuid(), DateTime.UtcNow));
    }

    [Fact]
    public void Resolve_throws_when_already_resolved()
    {
        var conflict = new SyncConflict();
        conflict.Resolve("Keep local data", Guid.NewGuid(), DateTime.UtcNow);

        Assert.Throws<InvalidOperationException>(() =>
            conflict.Resolve("Use server data", Guid.NewGuid(), DateTime.UtcNow));
    }

    [Fact]
    public void New_sync_device_has_expected_defaults()
    {
        var device = new SyncDevice();

        Assert.True(device.IsActive);
        Assert.Null(device.LastSuccessfulSyncUtc);
        Assert.Equal(string.Empty, device.DeviceName);
        Assert.Equal(string.Empty, device.DeviceCode);
    }
}
