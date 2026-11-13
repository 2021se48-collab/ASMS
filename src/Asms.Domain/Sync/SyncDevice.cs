using Asms.Domain.Common;

namespace Asms.Domain.Sync;

/// <summary>
/// Represents a device that syncs school data.
/// </summary>
public class SyncDevice : BaseEntity
{
    public string DeviceName { get; set; } = string.Empty;

    public string DeviceCode { get; set; } = string.Empty;

    public string? Platform { get; set; }

    public DateTime? LastSuccessfulSyncUtc { get; set; }

    public bool IsActive { get; set; } = true;
}
