using Asms.Domain.Common;

namespace Asms.Domain.Security;

public class AppRole : BaseEntity
{
    public string Name { get; set; } = string.Empty;

    public string? Description { get; set; }

    public bool IsSystemRole { get; set; }
}
