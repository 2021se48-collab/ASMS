using Asms.Domain.Common;
using Asms.Domain.Enums;

namespace Asms.Domain.Examinations;

/// <summary>
/// Represents an examination in an academic year.
/// </summary>
public class Examination : BaseEntity
{
    public Guid AcademicYearId { get; set; }

    public string Name { get; set; } = string.Empty;

    public ExaminationType Type { get; set; } = ExaminationType.Other;

    public DateOnly StartDate { get; set; }

    public DateOnly? EndDate { get; set; }

    public ExaminationStatus Status { get; set; } = ExaminationStatus.Draft;
}
