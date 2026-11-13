using Asms.Domain.Common;

namespace Asms.Domain.Security;

/// <summary>
/// Stores a user profile. No passwords are stored here.
/// </summary>
public class AppUser : BaseEntity
{
    public string FullName { get; set; } = string.Empty;

    public string? Email { get; set; }

    public string? PhoneNumber { get; set; }

    public string PreferredLanguage { get; set; } = "en";

    public bool IsActive { get; set; } = true;

    public Guid? IdentityUserId { get; set; }
}
