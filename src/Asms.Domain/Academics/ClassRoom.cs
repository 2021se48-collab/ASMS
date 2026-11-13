using Asms.Domain.Common;

namespace Asms.Domain.Academics;

/// <summary>
/// Represents a classroom for a grade and academic year.
/// </summary>
public class ClassRoom : BaseEntity
{
    public Guid GradeId { get; set; }

    public Guid AcademicYearId { get; set; }

    public Guid? BranchId { get; set; }

    public string Name { get; set; } = string.Empty;

    public int? Capacity { get; set; }
}
