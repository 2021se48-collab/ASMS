using Asms.Domain.Schools;

namespace Asms.Domain.Tests.Schools;

public class SchoolTests
{
    [Fact]
    public void New_school_gets_a_non_empty_Id()
    {
        var school = new School();

        Assert.NotEqual(Guid.Empty, school.Id);
    }

    [Fact]
    public void Two_new_schools_get_different_Ids()
    {
        var firstSchool = new School();
        var secondSchool = new School();

        Assert.NotEqual(firstSchool.Id, secondSchool.Id);
    }

    [Fact]
    public void New_school_starts_at_Version_1()
    {
        var school = new School();

        Assert.Equal(1, school.Version);
    }

    [Fact]
    public void New_school_is_active()
    {
        var school = new School();

        Assert.True(school.IsActive);
    }

    [Fact]
    public void New_school_is_not_deleted()
    {
        var school = new School();

        Assert.False(school.IsDeleted);
    }

    [Fact]
    public void New_school_uses_English_by_default()
    {
        var school = new School();

        Assert.Equal("en", school.DefaultLanguage);
    }

    [Fact]
    public void New_school_uses_the_Solar_Hijri_calendar_by_default()
    {
        var school = new School();

        Assert.Equal("SolarHijri", school.CalendarType);
    }

    [Fact]
    public void New_school_has_UTC_timestamps()
    {
        var school = new School();

        Assert.Equal(DateTimeKind.Utc, school.CreatedAtUtc.Kind);
        Assert.Equal(DateTimeKind.Utc, school.UpdatedAtUtc.Kind);
    }
}
