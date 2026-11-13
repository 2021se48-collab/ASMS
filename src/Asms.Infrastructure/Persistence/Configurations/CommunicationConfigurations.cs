using Asms.Domain.Communication;
using Asms.Domain.Schools;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Asms.Infrastructure.Persistence.Configurations;

public sealed class NotificationConfiguration : IEntityTypeConfiguration<Notification>
{
    public void Configure(EntityTypeBuilder<Notification> builder)
    {
        builder.Property(notification => notification.TemplateKey).IsRequired().HasMaxLength(200);
        builder.Property(notification => notification.RelatedEntityType).HasMaxLength(200);
        builder.HasIndex(notification => notification.SchoolId)
            .HasDatabaseName("IX_Notifications_SchoolId");
        builder.HasIndex(notification => new
            {
                notification.SchoolId,
                notification.RecipientUserId,
                notification.IsRead
            })
            .HasDatabaseName("IX_Notifications_SchoolId_RecipientUserId_IsRead");
        builder.HasOne<School>().WithMany().HasForeignKey(notification => notification.SchoolId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class AnnouncementConfiguration : IEntityTypeConfiguration<Announcement>
{
    public void Configure(EntityTypeBuilder<Announcement> builder)
    {
        builder.Property(announcement => announcement.Title).IsRequired().HasMaxLength(200);
        builder.Property(announcement => announcement.Body).IsRequired().HasMaxLength(2000);
        builder.Property(announcement => announcement.LanguageCode).HasMaxLength(10);
        builder.HasIndex(announcement => announcement.SchoolId)
            .HasDatabaseName("IX_Announcements_SchoolId");
        builder.HasIndex(announcement => new { announcement.SchoolId, announcement.PublishedAtUtc })
            .HasDatabaseName("IX_Announcements_SchoolId_PublishedAtUtc");
        builder.HasOne<School>().WithMany().HasForeignKey(announcement => announcement.SchoolId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
