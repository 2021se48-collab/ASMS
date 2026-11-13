namespace Asms.Domain.Enums;

/// <summary>
/// Describes the sync state of a record.
/// </summary>
public enum SyncStatus
{
    /// <summary>The record is waiting to be sent.</summary>
    Pending = 0,

    /// <summary>The record was sent successfully.</summary>
    Sent = 1,

    /// <summary>The record could not be sent.</summary>
    Failed = 2,

    /// <summary>The record has a sync conflict.</summary>
    Conflict = 3
}