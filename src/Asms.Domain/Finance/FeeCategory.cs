using Asms.Domain.Common;

namespace Asms.Domain.Finance;

/// <summary>
/// Groups fees into a category.
/// </summary>
public class FeeCategory : BaseEntity
{
    public string Name { get; set; } = string.Empty;

    public string? Description { get; set; }

    public bool IsActive { get; set; } = true;
}
