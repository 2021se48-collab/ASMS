using Microsoft.AspNetCore.Identity;

namespace Asms.Infrastructure.Identity;

/// <summary>
/// This is the login account only. Identity stores passwords as salted hashes; profile data lives in AppUser.
/// </summary>
public class AsmsIdentityUser : IdentityUser<Guid>
{
    public Guid SchoolId { get; set; }

    public Guid? AppUserId { get; set; }

    public bool IsActive { get; set; } = true;

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

    public DateTime? LastLoginAtUtc { get; set; }
}
