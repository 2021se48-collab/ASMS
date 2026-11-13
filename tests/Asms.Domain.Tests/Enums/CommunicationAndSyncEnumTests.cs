using Asms.Domain.Enums;

namespace Asms.Domain.Tests.Enums;

public class CommunicationAndSyncEnumTests
{
    [Fact]
    public void Communication_and_sync_enum_values_match_database_values()
    {
        Assert.Equal(0, (int)NotificationPriority.Normal);
        Assert.Equal(1, (int)NotificationPriority.Important);
        Assert.Equal(2, (int)NotificationPriority.Urgent);

        Assert.Equal(0, (int)NotificationChannel.InApp);
        Assert.Equal(1, (int)NotificationChannel.Sms);

        Assert.Equal(0, (int)AnnouncementAudience.All);
        Assert.Equal(1, (int)AnnouncementAudience.Teachers);
        Assert.Equal(2, (int)AnnouncementAudience.Parents);
        Assert.Equal(3, (int)AnnouncementAudience.Students);
        Assert.Equal(4, (int)AnnouncementAudience.Staff);

        Assert.Equal(0, (int)SyncOperationType.Create);
        Assert.Equal(1, (int)SyncOperationType.Update);
        Assert.Equal(2, (int)SyncOperationType.Delete);
    }
}
