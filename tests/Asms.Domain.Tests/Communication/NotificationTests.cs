using Asms.Domain.Communication;
using Asms.Domain.Common;
using Asms.Domain.Enums;

namespace Asms.Domain.Tests.Communication;

public class NotificationTests
{
    [Fact]
    public void Notification_implements_ITenantEntity()
    {
        Assert.IsAssignableFrom<ITenantEntity>(new Notification());
    }

    [Fact]
    public void New_notification_has_expected_defaults()
    {
        var notification = new Notification();

        Assert.Equal(string.Empty, notification.TemplateKey);
        Assert.Equal(NotificationPriority.Normal, notification.Priority);
        Assert.Equal(NotificationChannel.InApp, notification.Channel);
        Assert.False(notification.IsRead);
    }

    [Fact]
    public void MarkAsRead_sets_read_state_and_time()
    {
        var notification = new Notification();
        var readAtUtc = new DateTime(2026, 11, 13, 9, 0, 0, DateTimeKind.Utc);

        notification.MarkAsRead(readAtUtc);

        Assert.True(notification.IsRead);
        Assert.Equal(readAtUtc, notification.ReadAtUtc);
    }

    [Fact]
    public void MarkAsRead_keeps_the_first_read_time()
    {
        var notification = new Notification();
        var firstReadAtUtc = new DateTime(2026, 11, 13, 9, 0, 0, DateTimeKind.Utc);

        notification.MarkAsRead(firstReadAtUtc);
        notification.MarkAsRead(firstReadAtUtc.AddHours(1));

        Assert.Equal(firstReadAtUtc, notification.ReadAtUtc);
    }
}
