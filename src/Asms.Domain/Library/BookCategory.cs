using Asms.Domain.Common;

namespace Asms.Domain.Library;

/// <summary>
/// Groups books into categories.
/// </summary>
public class BookCategory : BaseEntity
{
    public string Name { get; set; } = string.Empty;

    public string? Description { get; set; }
}
