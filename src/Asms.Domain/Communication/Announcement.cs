using Asms.Domain.Common;
using Asms.Domain.Enums;

namespace Asms.Domain.Communication;

/// <summary>
/// Stores text written by the school and never auto-translated. LanguageCode is en, ps, or fa-AF.
/// </summary>
public class Announcement : BaseEntity
{
    public string Title { get; set; } = string.Empty;

    public string Body { get; set; } = string.Empty;

    public string? LanguageCode { get; set; }

    public AnnouncementAudience Audience { get; set; } = AnnouncementAudience.All;

    public DateTime? PublishedAtUtc { get; set; }

    public DateTime? ExpiresAtUtc { get; set; }

    public bool IsActiveAt(DateTime utcNow) =>
        PublishedAtUtc != null
        && PublishedAtUtc <= utcNow
        && (ExpiresAtUtc == null || ExpiresAtUtc > utcNow)
        && !IsDeleted;
}
