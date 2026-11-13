using Asms.Domain.Staffing;

namespace Asms.Domain.Tests.Staffing;

public class LeaveRequestTests
{
    [Fact]
    public void HasValidDateRange_is_true_for_equal_dates()
    {
        var date = new DateOnly(2026, 11, 13);
        var request = new LeaveRequest { StartDate = date, EndDate = date };

        Assert.True(request.HasValidDateRange());
    }

    [Fact]
    public void HasValidDateRange_is_true_when_end_date_is_after_start_date()
    {
        var request = new LeaveRequest
        {
            StartDate = new DateOnly(2026, 11, 9),
            EndDate = new DateOnly(2026, 11, 13)
        };

        Assert.True(request.HasValidDateRange());
    }

    [Fact]
    public void HasValidDateRange_is_false_when_end_date_is_before_start_date()
    {
        var request = new LeaveRequest
        {
            StartDate = new DateOnly(2026, 11, 13),
            EndDate = new DateOnly(2026, 11, 9)
        };

        Assert.False(request.HasValidDateRange());
    }

    [Fact]
    public void TotalDays_counts_inclusive_date_ranges()
    {
        var singleDay = new LeaveRequest
        {
            StartDate = new DateOnly(2026, 11, 9),
            EndDate = new DateOnly(2026, 11, 9)
        };
        var workWeek = new LeaveRequest
        {
            StartDate = new DateOnly(2026, 11, 9),
            EndDate = new DateOnly(2026, 11, 13)
        };
        var monthBoundary = new LeaveRequest
        {
            StartDate = new DateOnly(2026, 1, 30),
            EndDate = new DateOnly(2026, 2, 3)
        };

        Assert.Equal(1, singleDay.TotalDays());
        Assert.Equal(5, workWeek.TotalDays());
        Assert.Equal(5, monthBoundary.TotalDays());
    }

    [Fact]
    public void TotalDays_throws_for_an_invalid_date_range()
    {
        var request = new LeaveRequest
        {
            StartDate = new DateOnly(2026, 11, 14),
            EndDate = new DateOnly(2026, 11, 13)
        };

        Assert.Throws<InvalidOperationException>(() => request.TotalDays());
    }
}
