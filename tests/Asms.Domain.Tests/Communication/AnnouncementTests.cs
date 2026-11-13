using Asms.Domain.Communication;
using Asms.Domain.Common;
using Asms.Domain.Enums;

namespace Asms.Domain.Tests.Communication;

public class AnnouncementTests
{
    [Fact]
    public void Announcement_implements_ITenantEntity()
    {
        Assert.IsAssignableFrom<ITenantEntity>(new Announcement());
    }

    [Fact]
    public void New_announcement_has_expected_defaults()
    {
        var announcement = new Announcement();

        Assert.Equal(string.Empty, announcement.Title);
        Assert.Equal(string.Empty, announcement.Body);
        Assert.Null(announcement.LanguageCode);
        Assert.Equal(AnnouncementAudience.All, announcement.Audience);
        Assert.Null(announcement.PublishedAtUtc);
        Assert.Null(announcement.ExpiresAtUtc);
    }

    [Fact]
    public void IsActiveAt_returns_false_when_not_published_or_before_publication()
    {
        var utcNow = new DateTime(2026, 11, 13, 9, 0, 0, DateTimeKind.Utc);
        var notPublished = new Announcement();
        var notYetPublished = new Announcement
        {
            PublishedAtUtc = utcNow.AddMinutes(1)
        };

        Assert.False(notPublished.IsActiveAt(utcNow));
        Assert.False(notYetPublished.IsActiveAt(utcNow));
    }

    [Fact]
    public void IsActiveAt_returns_true_between_publication_and_expiry()
    {
        var utcNow = new DateTime(2026, 11, 13, 9, 0, 0, DateTimeKind.Utc);
        var announcement = new Announcement
        {
            PublishedAtUtc = utcNow.AddMinutes(-1),
            ExpiresAtUtc = utcNow.AddMinutes(1)
        };

        Assert.True(announcement.IsActiveAt(utcNow));
    }

    [Fact]
    public void IsActiveAt_returns_false_after_expiry()
    {
        var utcNow = new DateTime(2026, 11, 13, 9, 0, 0, DateTimeKind.Utc);
        var announcement = new Announcement
        {
            PublishedAtUtc = utcNow.AddHours(-1),
            ExpiresAtUtc = utcNow.AddMinutes(-1)
        };

        Assert.False(announcement.IsActiveAt(utcNow));
    }

    [Fact]
    public void IsActiveAt_returns_true_without_expiry_and_false_when_deleted()
    {
        var utcNow = new DateTime(2026, 11, 13, 9, 0, 0, DateTimeKind.Utc);
        var noExpiry = new Announcement { PublishedAtUtc = utcNow.AddHours(-1) };
        var deleted = new Announcement { PublishedAtUtc = utcNow.AddHours(-1), IsDeleted = true };

        Assert.True(noExpiry.IsActiveAt(utcNow));
        Assert.False(deleted.IsActiveAt(utcNow));
    }
}
