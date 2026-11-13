using Asms.Domain.Academics;

namespace Asms.Domain.Tests.Academics;

public class AcademicYearTests
{
    [Fact]
    public void HasValidDateRange_is_true_when_end_date_is_after_start_date()
    {
        var academicYear = new AcademicYear
        {
            StartDate = new DateOnly(2025, 3, 1),
            EndDate = new DateOnly(2026, 2, 28)
        };

        Assert.True(academicYear.HasValidDateRange());
    }

    [Fact]
    public void HasValidDateRange_is_false_when_dates_are_equal()
    {
        var date = new DateOnly(2025, 3, 1);
        var academicYear = new AcademicYear { StartDate = date, EndDate = date };

        Assert.False(academicYear.HasValidDateRange());
    }

    [Fact]
    public void HasValidDateRange_is_false_when_end_date_is_before_start_date()
    {
        var academicYear = new AcademicYear
        {
            StartDate = new DateOnly(2026, 2, 28),
            EndDate = new DateOnly(2025, 3, 1)
        };

        Assert.False(academicYear.HasValidDateRange());
    }

    [Fact]
    public void Contains_includes_start_end_and_middle_dates()
    {
        var academicYear = new AcademicYear
        {
            StartDate = new DateOnly(2025, 3, 1),
            EndDate = new DateOnly(2026, 2, 28)
        };

        Assert.True(academicYear.Contains(new DateOnly(2025, 3, 1)));
        Assert.True(academicYear.Contains(new DateOnly(2025, 9, 1)));
        Assert.True(academicYear.Contains(new DateOnly(2026, 2, 28)));
    }

    [Fact]
    public void Contains_excludes_the_day_before_and_after_the_date_range()
    {
        var academicYear = new AcademicYear
        {
            StartDate = new DateOnly(2025, 3, 1),
            EndDate = new DateOnly(2026, 2, 28)
        };

        Assert.False(academicYear.Contains(new DateOnly(2025, 2, 28)));
        Assert.False(academicYear.Contains(new DateOnly(2026, 3, 1)));
    }

    [Fact]
    public void New_academic_year_has_default_flags_and_empty_name()
    {
        var academicYear = new AcademicYear();

        Assert.False(academicYear.IsCurrent);
        Assert.False(academicYear.IsClosed);
        Assert.Equal(string.Empty, academicYear.Name);
    }
}
