using Asms.Domain.Common;

namespace Asms.Domain.Students;

public class Guardian : BaseEntity
{
    public string FullName { get; set; } = string.Empty;

    public string? PhoneNumber { get; set; }

    public string? Email { get; set; }

    public string? NationalIdNumber { get; set; }

    public string? Address { get; set; }

    public string? Occupation { get; set; }

    public Guid? AppUserId { get; set; }
}
