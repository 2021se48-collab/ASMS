using Asms.Domain.Common;

namespace Asms.Domain.Examinations;

/// <summary>
/// Defines a grade and its percentage range.
/// </summary>
public class GradeScaleItem : BaseEntity
{
    public Guid GradeScaleId { get; set; }

    public string GradeName { get; set; } = string.Empty;

    public decimal MinPercentage { get; set; }

    public decimal MaxPercentage { get; set; }

    public bool IsPass { get; set; }

    public int SortOrder { get; set; }
}
