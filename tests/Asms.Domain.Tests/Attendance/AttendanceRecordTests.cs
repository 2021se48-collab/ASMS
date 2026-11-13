using Asms.Domain.Attendance;
using Asms.Domain.Common;
using Asms.Domain.Enums;

namespace Asms.Domain.Tests.Attendance;

public class AttendanceRecordTests
{
    [Fact]
    public void Attendance_record_implements_ITenantEntity()
    {
        Assert.IsAssignableFrom<ITenantEntity>(new AttendanceRecord());
    }

    [Fact]
    public void New_attendance_record_has_default_status_and_optional_values()
    {
        var record = new AttendanceRecord();

        Assert.Equal(AttendanceStatus.Present, record.Status);
        Assert.Null(record.SectionId);
        Assert.Null(record.Remarks);
    }

    [Fact]
    public void New_attendance_record_has_empty_SchoolId_until_assigned()
    {
        Assert.Equal(Guid.Empty, new AttendanceRecord().SchoolId);
    }
}
