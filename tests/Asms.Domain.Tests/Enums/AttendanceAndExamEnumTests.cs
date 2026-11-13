using Asms.Domain.Enums;

namespace Asms.Domain.Tests.Enums;

public class AttendanceAndExamEnumTests
{
    [Fact]
    public void Attendance_and_examination_enum_values_match_database_values()
    {
        Assert.Equal(0, (int)AttendanceStatus.Present);
        Assert.Equal(1, (int)AttendanceStatus.Absent);
        Assert.Equal(2, (int)AttendanceStatus.Late);
        Assert.Equal(3, (int)AttendanceStatus.Excused);

        Assert.Equal(0, (int)ExaminationType.Monthly);
        Assert.Equal(1, (int)ExaminationType.Quarterly);
        Assert.Equal(2, (int)ExaminationType.Midterm);
        Assert.Equal(3, (int)ExaminationType.Final);
        Assert.Equal(4, (int)ExaminationType.Other);

        Assert.Equal(0, (int)ExaminationStatus.Draft);
        Assert.Equal(1, (int)ExaminationStatus.Open);
        Assert.Equal(2, (int)ExaminationStatus.Finalized);
        Assert.Equal(3, (int)ExaminationStatus.Approved);
    }
}
