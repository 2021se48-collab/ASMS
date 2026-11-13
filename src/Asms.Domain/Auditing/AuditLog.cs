using Asms.Domain.Common;

namespace Asms.Domain.Auditing;

/// <summary>
/// Audit logs are append-only and never edited or deleted. SchoolId may be empty for platform actions.
/// </summary>
public class AuditLog : BaseEntity
{
    public Guid? UserId { get; init; }

    public string Action { get; init; } = string.Empty;

    public string EntityType { get; init; } = string.Empty;

    public Guid? EntityId { get; init; }

    public string? OldValuesJson { get; init; }

    public string? NewValuesJson { get; init; }

    public string? IpAddress { get; init; }

    public Guid? DeviceId { get; init; }

    public DateTime OccurredAtUtc { get; init; }
}
