using Asms.Domain.Common;

namespace Asms.Domain.Schools;

public class SchoolBranch : BaseEntity
{
    public string Name { get; set; } = string.Empty;

    public string? Code { get; set; }

    public string? Province { get; set; }

    public string? District { get; set; }

    public string? Address { get; set; }

    public string? PhoneNumber { get; set; }

    public bool IsMainBranch { get; set; }

    public bool IsActive { get; set; } = true;
}
