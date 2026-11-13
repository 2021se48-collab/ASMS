using Asms.Domain.Common;

namespace Asms.Domain.Examinations;

/// <summary>
/// An absent student is not a zero; marks are null when absent and locked when the exam is finalized.
/// </summary>
public class StudentMark : BaseEntity
{
    public Guid ExaminationSubjectId { get; set; }

    public Guid StudentId { get; set; }

    public decimal? ObtainedMarks { get; set; }

    public bool IsAbsent { get; set; }

    public string? Remarks { get; set; }

    public bool IsLocked { get; set; }
}
