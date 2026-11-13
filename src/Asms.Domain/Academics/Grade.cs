using Asms.Domain.Common;

namespace Asms.Domain.Academics;

/// <summary>
/// Represents a grade in a school.
/// </summary>
public class Grade : BaseEntity
{
    public string Name { get; set; } = string.Empty;

    public int Level { get; set; }

    public int SortOrder { get; set; }

    public bool IsActive { get; set; } = true;
}
