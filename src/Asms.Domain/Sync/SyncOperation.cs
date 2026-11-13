using Asms.Domain.Common;
using Asms.Domain.Enums;

namespace Asms.Domain.Sync;

/// <summary>
/// Represents an offline queue operation. IdempotencyKey prevents duplicate sends on retry.
/// </summary>
public class SyncOperation : BaseEntity
{
    public Guid DeviceId { get; set; }

    public string EntityType { get; set; } = string.Empty;

    public Guid EntityId { get; set; }

    public SyncOperationType OperationType { get; set; }

    public string PayloadJson { get; set; } = string.Empty;

    public long EntityVersion { get; set; }

    public Guid IdempotencyKey { get; set; } = Guid.NewGuid();

    public SyncStatus Status { get; set; } = SyncStatus.Pending;

    public int RetryCount { get; set; }

    public string? LastError { get; set; }

    public DateTime? SentAtUtc { get; set; }

    public void MarkSent(DateTime sentAtUtc)
    {
        if (Status == SyncStatus.Conflict)
        {
            throw new InvalidOperationException();
        }

        Status = SyncStatus.Sent;
        SentAtUtc = sentAtUtc;
        LastError = null;
    }

    public void MarkFailed(string error)
    {
        if (string.IsNullOrWhiteSpace(error))
        {
            throw new ArgumentException("Error cannot be blank.", nameof(error));
        }

        if (Status == SyncStatus.Sent)
        {
            throw new InvalidOperationException();
        }

        Status = SyncStatus.Failed;
        RetryCount++;
        LastError = error;
    }

    public void MarkConflict(string reason)
    {
        if (string.IsNullOrWhiteSpace(reason))
        {
            throw new ArgumentException("Reason cannot be blank.", nameof(reason));
        }

        Status = SyncStatus.Conflict;
        LastError = reason;
    }

    public bool ShouldRetry(int maxRetries) => Status == SyncStatus.Failed && RetryCount < maxRetries;
}
