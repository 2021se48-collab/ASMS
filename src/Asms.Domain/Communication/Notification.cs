using Asms.Domain.Common;
using Asms.Domain.Enums;

namespace Asms.Domain.Communication;

/// <summary>
/// Stores a template key, not finished text, so the app can show it in the user's language.
/// </summary>
public class Notification : BaseEntity
{
    public Guid? RecipientUserId { get; set; }

    public string TemplateKey { get; set; } = string.Empty;

    public string? ParametersJson { get; set; }

    public NotificationPriority Priority { get; set; } = NotificationPriority.Normal;

    public NotificationChannel Channel { get; set; } = NotificationChannel.InApp;

    public bool IsRead { get; set; }

    public DateTime? ReadAtUtc { get; set; }

    public string? RelatedEntityType { get; set; }

    public Guid? RelatedEntityId { get; set; }

    public void MarkAsRead(DateTime readAtUtc)
    {
        if (IsRead)
        {
            return;
        }

        IsRead = true;
        ReadAtUtc = readAtUtc;
    }
}
