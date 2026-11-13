namespace Asms.Domain.Common;

/// <summary>
/// Provides common fields for domain entities.
/// </summary>
public abstract class BaseEntity : ITenantEntity
{
    /// <summary>
    /// Gets the unique ID generated on this device so offline computers never collide.
    /// </summary>
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>
    /// Gets or sets the school that owns this record.
    /// </summary>
    public Guid SchoolId { get; set; }

    /// <summary>
    /// Gets the time this record was created in UTC.
    /// </summary>
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Gets the time this record was last updated in UTC.
    /// </summary>
    public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Gets the version used to detect sync conflicts.
    /// </summary>
    public long Version { get; set; } = 1;

    /// <summary>
    /// Gets whether this record is soft deleted, so it is never erased.
    /// </summary>
    public bool IsDeleted { get; set; }

    /// <summary>
    /// Gets or sets the ID of the user who created this record.
    /// </summary>
    public Guid? CreatedByUserId { get; set; }

    /// <summary>
    /// Gets or sets the ID of the user who last updated this record.
    /// </summary>
    public Guid? UpdatedByUserId { get; set; }

    /// <summary>
    /// Gets or sets the ID of the device that last modified this record.
    /// </summary>
    public Guid? LastModifiedByDeviceId { get; set; }
}