using Asms.Domain.Enums;

namespace Asms.Domain.Tests.Enums;

public class StudentEnumsTests
{
    [Fact]
    public void Student_enum_values_match_database_values()
    {
        Assert.Equal(0, (int)Gender.Male);
        Assert.Equal(1, (int)Gender.Female);

        Assert.Equal(0, (int)StudentStatus.Active);
        Assert.Equal(1, (int)StudentStatus.Transferred);
        Assert.Equal(2, (int)StudentStatus.Graduated);
        Assert.Equal(3, (int)StudentStatus.Withdrawn);

        Assert.Equal(0, (int)GuardianRelationship.Father);
        Assert.Equal(1, (int)GuardianRelationship.Mother);
        Assert.Equal(2, (int)GuardianRelationship.Brother);
        Assert.Equal(3, (int)GuardianRelationship.Sister);
        Assert.Equal(4, (int)GuardianRelationship.Uncle);
        Assert.Equal(5, (int)GuardianRelationship.Aunt);
        Assert.Equal(6, (int)GuardianRelationship.Grandfather);
        Assert.Equal(7, (int)GuardianRelationship.Grandmother);
        Assert.Equal(8, (int)GuardianRelationship.Other);
    }
}
