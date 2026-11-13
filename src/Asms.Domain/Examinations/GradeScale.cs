using Asms.Domain.Common;

namespace Asms.Domain.Examinations;

/// <summary>
/// Defines a school's grading scale.
/// </summary>
public class GradeScale : BaseEntity
{
    public string Name { get; set; } = string.Empty;

    public bool IsDefault { get; set; }
}
