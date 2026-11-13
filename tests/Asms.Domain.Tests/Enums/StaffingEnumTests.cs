using Asms.Domain.Enums;

namespace Asms.Domain.Tests.Enums;

public class StaffingEnumTests
{
    [Fact]
    public void Staffing_enum_values_match_database_values()
    {
        Assert.Equal(0, (int)EmploymentStatus.Active);
        Assert.Equal(1, (int)EmploymentStatus.OnLeave);
        Assert.Equal(2, (int)EmploymentStatus.Resigned);
        Assert.Equal(3, (int)EmploymentStatus.Terminated);

        Assert.Equal(0, (int)EmployeeType.Teacher);
        Assert.Equal(1, (int)EmployeeType.Staff);

        Assert.Equal(0, (int)LeaveType.Annual);
        Assert.Equal(1, (int)LeaveType.Sick);
        Assert.Equal(2, (int)LeaveType.Maternity);
        Assert.Equal(3, (int)LeaveType.Unpaid);
        Assert.Equal(4, (int)LeaveType.Other);

        Assert.Equal(0, (int)LeaveStatus.Pending);
        Assert.Equal(1, (int)LeaveStatus.Approved);
        Assert.Equal(2, (int)LeaveStatus.Rejected);
        Assert.Equal(3, (int)LeaveStatus.Cancelled);

        Assert.Equal(0, (int)SalaryStatus.Draft);
        Assert.Equal(1, (int)SalaryStatus.Approved);
        Assert.Equal(2, (int)SalaryStatus.Paid);
    }
}
