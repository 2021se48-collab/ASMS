using Asms.Domain.Common;

namespace Asms.Domain.Academics;

/// <summary>
/// Represents a subject taught at a school.
/// </summary>
public class Subject : BaseEntity
{
    public string Name { get; set; } = string.Empty;

    public string? Code { get; set; }

    public bool IsActive { get; set; } = true;
}
