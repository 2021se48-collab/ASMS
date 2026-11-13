using Asms.Domain.Common;
using Asms.Domain.Enums;

namespace Asms.Domain.Staffing;

/// <summary>
/// Stores one attendance record per teacher per date.
/// </summary>
public class TeacherAttendanceRecord : BaseEntity
{
    public Guid TeacherId { get; set; }

    public DateOnly Date { get; set; }

    public AttendanceStatus Status { get; set; } = AttendanceStatus.Present;

    public TimeOnly? CheckInTime { get; set; }

    public TimeOnly? CheckOutTime { get; set; }

    public string? Remarks { get; set; }
}
