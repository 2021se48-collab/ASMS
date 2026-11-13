using Asms.Domain.Common;

namespace Asms.Domain.Finance;

/// <summary>
/// Defines a fee for an academic year.
/// </summary>
public class FeeStructure : BaseEntity
{
    public Guid FeeCategoryId { get; set; }

    public Guid AcademicYearId { get; set; }

    public Guid? GradeId { get; set; }

    public decimal Amount { get; set; }

    public string Currency { get; set; } = "AFN";
}
