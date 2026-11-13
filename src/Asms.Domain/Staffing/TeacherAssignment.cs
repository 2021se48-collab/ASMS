using Asms.Domain.Common;

namespace Asms.Domain.Staffing;

/// <summary>
/// A teacher may manage only classes and subjects listed in this assignment.
/// </summary>
public class TeacherAssignment : BaseEntity
{
    public Guid TeacherId { get; set; }

    public Guid SubjectId { get; set; }

    public Guid ClassRoomId { get; set; }

    public Guid? SectionId { get; set; }

    public Guid AcademicYearId { get; set; }

    public int? WeeklyPeriods { get; set; }
}
