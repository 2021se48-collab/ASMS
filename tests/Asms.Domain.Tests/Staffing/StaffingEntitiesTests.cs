using Asms.Domain.Common;
using Asms.Domain.Enums;
using Asms.Domain.Staffing;

namespace Asms.Domain.Tests.Staffing;

public class StaffingEntitiesTests
{
    [Fact]
    public void Staffing_entities_implement_ITenantEntity()
    {
        Assert.IsAssignableFrom<ITenantEntity>(new Teacher());
        Assert.IsAssignableFrom<ITenantEntity>(new StaffMember());
        Assert.IsAssignableFrom<ITenantEntity>(new TeacherAssignment());
        Assert.IsAssignableFrom<ITenantEntity>(new TeacherAttendanceRecord());
        Assert.IsAssignableFrom<ITenantEntity>(new LeaveRequest());
        Assert.IsAssignableFrom<ITenantEntity>(new SalaryRecord());
    }

    [Fact]
    public void Teacher_has_expected_defaults()
    {
        var teacher = new Teacher();

        Assert.Equal(EmploymentStatus.Active, teacher.Status);
        Assert.Equal(string.Empty, teacher.EmployeeNumber);
        Assert.Equal(string.Empty, teacher.FirstName);
        Assert.Equal(string.Empty, teacher.LastName);
        Assert.Null(teacher.AppUserId);
    }

    [Fact]
    public void Staff_member_name_and_employee_number_defaults_are_empty()
    {
        var staffMember = new StaffMember();

        Assert.Equal(string.Empty, staffMember.EmployeeNumber);
        Assert.Equal(string.Empty, staffMember.FirstName);
        Assert.Equal(string.Empty, staffMember.LastName);
    }

    [Fact]
    public void Teacher_attendance_has_default_status_and_no_check_in_time()
    {
        var attendance = new TeacherAttendanceRecord();

        Assert.Equal(AttendanceStatus.Present, attendance.Status);
        Assert.Null(attendance.CheckInTime);
    }

    [Fact]
    public void Leave_request_has_default_status_and_type()
    {
        var leaveRequest = new LeaveRequest();

        Assert.Equal(LeaveStatus.Pending, leaveRequest.Status);
        Assert.Equal(LeaveType.Annual, leaveRequest.LeaveType);
    }

    [Fact]
    public void Salary_record_has_expected_defaults()
    {
        var salaryRecord = new SalaryRecord();

        Assert.Equal(SalaryStatus.Draft, salaryRecord.Status);
        Assert.Equal("AFN", salaryRecord.Currency);
        Assert.Null(salaryRecord.PaidAtUtc);
    }

    [Fact]
    public void Teacher_assignment_section_is_optional()
    {
        Assert.Null(new TeacherAssignment().SectionId);
    }
}
