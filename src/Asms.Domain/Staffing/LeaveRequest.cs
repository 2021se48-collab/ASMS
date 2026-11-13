using Asms.Domain.Common;
using Asms.Domain.Enums;

namespace Asms.Domain.Staffing;

/// <summary>
/// Records a leave request for a teacher or staff member.
/// </summary>
public class LeaveRequest : BaseEntity
{
    public Guid EmployeeId { get; set; }

    public EmployeeType EmployeeType { get; set; }

    public LeaveType LeaveType { get; set; } = LeaveType.Annual;

    public DateOnly StartDate { get; set; }

    public DateOnly EndDate { get; set; }

    public string? Reason { get; set; }

    public LeaveStatus Status { get; set; } = LeaveStatus.Pending;

    public Guid? DecidedByUserId { get; set; }

    public DateTime? DecidedAtUtc { get; set; }

    public bool HasValidDateRange() => EndDate >= StartDate;

    public int TotalDays()
    {
        if (!HasValidDateRange())
        {
            throw new InvalidOperationException("The leave date range is invalid.");
        }

        return EndDate.DayNumber - StartDate.DayNumber + 1;
    }
}
