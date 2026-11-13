using Asms.Domain.Common;
using Asms.Domain.Enums;
using Asms.Domain.Students;

namespace Asms.Domain.Tests.Students;

public class StudentGuardianTests
{
    [Fact]
    public void Student_guardian_implements_ITenantEntity()
    {
        Assert.IsAssignableFrom<ITenantEntity>(new StudentGuardian());
    }

    [Fact]
    public void New_student_guardian_has_default_relationship_and_contact_flags()
    {
        var studentGuardian = new StudentGuardian();

        Assert.Equal(GuardianRelationship.Other, studentGuardian.Relationship);
        Assert.False(studentGuardian.IsPrimaryContact);
        Assert.False(studentGuardian.IsEmergencyContact);
    }
}
