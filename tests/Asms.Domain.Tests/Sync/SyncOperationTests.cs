using Asms.Domain.Enums;
using Asms.Domain.Sync;

namespace Asms.Domain.Tests.Sync;

public class SyncOperationTests
{
    [Fact]
    public void New_sync_operation_has_expected_defaults()
    {
        var operation = new SyncOperation();

        Assert.Equal(SyncStatus.Pending, operation.Status);
        Assert.Equal(0, operation.RetryCount);
        Assert.NotEqual(Guid.Empty, operation.IdempotencyKey);
    }

    [Fact]
    public void Sync_operations_get_different_idempotency_keys()
    {
        Assert.NotEqual(new SyncOperation().IdempotencyKey, new SyncOperation().IdempotencyKey);
    }

    [Fact]
    public void MarkSent_sets_status_time_and_clears_error()
    {
        var operation = new SyncOperation
        {
            Status = SyncStatus.Failed,
            LastError = "Previous error"
        };
        var sentAtUtc = new DateTime(2026, 11, 13, 9, 0, 0, DateTimeKind.Utc);

        operation.MarkSent(sentAtUtc);

        Assert.Equal(SyncStatus.Sent, operation.Status);
        Assert.Equal(sentAtUtc, operation.SentAtUtc);
        Assert.Null(operation.LastError);
    }

    [Fact]
    public void MarkFailed_increments_retry_count_and_stores_error()
    {
        var operation = new SyncOperation();

        operation.MarkFailed("First failure");
        Assert.Equal(1, operation.RetryCount);
        Assert.Equal("First failure", operation.LastError);

        operation.MarkFailed("Second failure");
        Assert.Equal(2, operation.RetryCount);
        Assert.Equal("Second failure", operation.LastError);
    }

    [Fact]
    public void MarkFailed_throws_for_blank_error_or_sent_operation()
    {
        Assert.Throws<ArgumentException>(() => new SyncOperation().MarkFailed(" "));

        var sentOperation = new SyncOperation { Status = SyncStatus.Sent };
        Assert.Throws<InvalidOperationException>(() => sentOperation.MarkFailed("Failure"));
    }

    [Fact]
    public void MarkSent_throws_for_conflict()
    {
        var operation = new SyncOperation { Status = SyncStatus.Conflict };

        Assert.Throws<InvalidOperationException>(() => operation.MarkSent(DateTime.UtcNow));
    }

    [Fact]
    public void MarkConflict_sets_status_and_reason()
    {
        var operation = new SyncOperation();

        operation.MarkConflict("Version mismatch");

        Assert.Equal(SyncStatus.Conflict, operation.Status);
        Assert.Equal("Version mismatch", operation.LastError);
    }

    [Fact]
    public void MarkConflict_throws_for_blank_reason()
    {
        Assert.Throws<ArgumentException>(() => new SyncOperation().MarkConflict(string.Empty));
    }

    [Fact]
    public void ShouldRetry_checks_status_and_retry_limit()
    {
        var failedOperation = new SyncOperation { Status = SyncStatus.Failed, RetryCount = 1 };
        var atLimitOperation = new SyncOperation { Status = SyncStatus.Failed, RetryCount = 3 };
        var pendingOperation = new SyncOperation { Status = SyncStatus.Pending };
        var sentOperation = new SyncOperation { Status = SyncStatus.Sent };

        Assert.True(failedOperation.ShouldRetry(3));
        Assert.False(atLimitOperation.ShouldRetry(3));
        Assert.False(pendingOperation.ShouldRetry(3));
        Assert.False(sentOperation.ShouldRetry(3));
    }
}
