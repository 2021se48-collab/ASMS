namespace Asms.Domain.Common;

/// <summary>
/// Marks an entity as belonging to a school.
/// </summary>
public interface ITenantEntity
{
    /// <summary>
    /// Gets or sets the school that owns this record.
    /// </summary>
    Guid SchoolId { get; set; }
}