using Asms.Domain.Common;

namespace Asms.Domain.Examinations;

/// <summary>
/// Represents a subject scheduled for an examination.
/// </summary>
public class ExaminationSubject : BaseEntity
{
    public Guid ExaminationId { get; set; }

    public Guid SubjectId { get; set; }

    public Guid ClassRoomId { get; set; }

    public decimal MaxMarks { get; set; }

    public decimal PassMarks { get; set; }

    public DateOnly? ExamDate { get; set; }
}
