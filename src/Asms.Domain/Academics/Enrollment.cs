using Asms.Domain.Common;
using Asms.Domain.Enums;

namespace Asms.Domain.Academics;

/// <summary>
/// Enrollment is history: create a new record each year and never edit or delete old records.
/// </summary>
public class Enrollment : BaseEntity
{
    public Guid StudentId { get; set; }

    public Guid AcademicYearId { get; set; }

    public Guid ClassRoomId { get; set; }

    public Guid? SectionId { get; set; }

    public DateOnly EnrollmentDate { get; set; }

    public DateOnly? EndDate { get; set; }

    public EnrollmentStatus Status { get; set; } = EnrollmentStatus.Enrolled;

    public string? Notes { get; set; }
}
