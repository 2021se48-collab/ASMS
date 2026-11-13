using Asms.Domain.Enums;

namespace Asms.Domain.Tests.Academics;

public class EnrollmentStatusTests
{
    [Fact]
    public void Enrollment_status_values_match_database_values()
    {
        Assert.Equal(0, (int)EnrollmentStatus.Enrolled);
        Assert.Equal(1, (int)EnrollmentStatus.Promoted);
        Assert.Equal(2, (int)EnrollmentStatus.Repeated);
        Assert.Equal(3, (int)EnrollmentStatus.Transferred);
        Assert.Equal(4, (int)EnrollmentStatus.Withdrawn);
        Assert.Equal(5, (int)EnrollmentStatus.Graduated);
    }
}
