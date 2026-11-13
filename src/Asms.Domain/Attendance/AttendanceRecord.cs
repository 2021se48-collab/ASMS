using Asms.Domain.Common;
using Asms.Domain.Enums;

namespace Asms.Domain.Attendance;

/// <summary>
/// Stores one attendance record per student, date, and class; the database enforces uniqueness.
/// </summary>
public class AttendanceRecord : BaseEntity
{
    public Guid StudentId { get; set; }

    public Guid ClassRoomId { get; set; }

    public Guid? SectionId { get; set; }

    public DateOnly Date { get; set; }

    public AttendanceStatus Status { get; set; } = AttendanceStatus.Present;

    public string? Remarks { get; set; }

    public Guid? RecordedByUserId { get; set; }
}
