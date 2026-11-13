using Asms.Domain.Common;
using Asms.Domain.Enums;
using Asms.Domain.Examinations;

namespace Asms.Domain.Tests.Examinations;

public class ExaminationEntitiesTests
{
    [Fact]
    public void Examination_entities_implement_ITenantEntity()
    {
        Assert.IsAssignableFrom<ITenantEntity>(new Examination());
        Assert.IsAssignableFrom<ITenantEntity>(new ExaminationSubject());
        Assert.IsAssignableFrom<ITenantEntity>(new StudentMark());
        Assert.IsAssignableFrom<ITenantEntity>(new GradeScale());
        Assert.IsAssignableFrom<ITenantEntity>(new GradeScaleItem());
    }

    [Fact]
    public void New_examination_has_default_values()
    {
        var examination = new Examination();

        Assert.Equal(string.Empty, examination.Name);
        Assert.Equal(ExaminationStatus.Draft, examination.Status);
        Assert.Equal(ExaminationType.Other, examination.Type);
        Assert.Null(examination.EndDate);
    }

    [Fact]
    public void New_student_mark_has_default_values()
    {
        var studentMark = new StudentMark();

        Assert.Null(studentMark.ObtainedMarks);
        Assert.False(studentMark.IsAbsent);
        Assert.False(studentMark.IsLocked);
    }

    [Fact]
    public void Grade_scale_defaults_are_empty_and_not_default()
    {
        var gradeScale = new GradeScale();
        var item = new GradeScaleItem();

        Assert.Equal(string.Empty, gradeScale.Name);
        Assert.False(gradeScale.IsDefault);
        Assert.Equal(string.Empty, item.GradeName);
    }
}
