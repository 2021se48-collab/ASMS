using Asms.Domain.Academics;
using Asms.Domain.Common;
using Asms.Domain.Enums;

namespace Asms.Domain.Tests.Academics;

public class AcademicEntitiesTests
{
    [Fact]
    public void Academic_entities_implement_ITenantEntity()
    {
        Assert.IsAssignableFrom<ITenantEntity>(new Grade());
        Assert.IsAssignableFrom<ITenantEntity>(new ClassRoom());
        Assert.IsAssignableFrom<ITenantEntity>(new Section());
        Assert.IsAssignableFrom<ITenantEntity>(new Subject());
        Assert.IsAssignableFrom<ITenantEntity>(new Enrollment());
    }

    [Fact]
    public void Grade_and_subject_are_active_by_default()
    {
        Assert.True(new Grade().IsActive);
        Assert.True(new Subject().IsActive);
    }

    [Fact]
    public void Academic_entity_names_default_to_empty_strings()
    {
        Assert.Equal(string.Empty, new Grade().Name);
        Assert.Equal(string.Empty, new ClassRoom().Name);
        Assert.Equal(string.Empty, new Section().Name);
        Assert.Equal(string.Empty, new Subject().Name);
    }

    [Fact]
    public void Enrollment_has_default_status_and_optional_values()
    {
        var enrollment = new Enrollment();

        Assert.Equal(EnrollmentStatus.Enrolled, enrollment.Status);
        Assert.Null(enrollment.SectionId);
        Assert.Null(enrollment.EndDate);
    }

    [Fact]
    public void Classroom_branch_is_optional()
    {
        Assert.Null(new ClassRoom().BranchId);
    }
}
