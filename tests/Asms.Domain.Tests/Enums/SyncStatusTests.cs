using Asms.Domain.Enums;

namespace Asms.Domain.Tests.Enums;

public class SyncStatusTests
{
    [Fact]
    public void Sync_status_values_match_database_values()
    {
        // These numeric values are stored in databases.
        Assert.Equal(0, (int)SyncStatus.Pending);
        Assert.Equal(1, (int)SyncStatus.Sent);
        Assert.Equal(2, (int)SyncStatus.Failed);
        Assert.Equal(3, (int)SyncStatus.Conflict);
    }
}
