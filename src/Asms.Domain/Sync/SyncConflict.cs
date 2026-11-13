using Asms.Domain.Common;

namespace Asms.Domain.Sync;

/// <summary>
/// Newer data is never silently overwritten; a person must resolve each conflict.
/// </summary>
public class SyncConflict : BaseEntity
{
    public Guid SyncOperationId { get; set; }

    public string EntityType { get; set; } = string.Empty;

    public Guid EntityId { get; set; }

    public long LocalVersion { get; set; }

    public long ServerVersion { get; set; }

    public string? LocalPayloadJson { get; set; }

    public string? ServerPayloadJson { get; set; }

    public bool IsResolved { get; set; }

    public string? Resolution { get; set; }

    public Guid? ResolvedByUserId { get; set; }

    public DateTime? ResolvedAtUtc { get; set; }

    public void Resolve(string resolution, Guid userId, DateTime resolvedAtUtc)
    {
        if (string.IsNullOrWhiteSpace(resolution))
        {
            throw new ArgumentException("Resolution cannot be blank.", nameof(resolution));
        }

        if (IsResolved)
        {
            throw new InvalidOperationException();
        }

        IsResolved = true;
        Resolution = resolution;
        ResolvedByUserId = userId;
        ResolvedAtUtc = resolvedAtUtc;
    }
}
