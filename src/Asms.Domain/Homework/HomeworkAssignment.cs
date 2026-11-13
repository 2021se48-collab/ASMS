using Asms.Domain.Common;

namespace Asms.Domain.Homework;

/// <summary>
/// Records homework assigned to a class.
/// </summary>
public class HomeworkAssignment : BaseEntity
{
    public Guid TeacherId { get; set; }

    public Guid ClassRoomId { get; set; }

    public Guid? SectionId { get; set; }

    public Guid SubjectId { get; set; }

    public string Title { get; set; } = string.Empty;

    public string? Description { get; set; }

    public DateOnly AssignedDate { get; set; }

    public DateOnly DueDate { get; set; }

    public bool AllowSubmissions { get; set; }

    public bool IsOverdue(DateOnly today) => today > DueDate;
}
