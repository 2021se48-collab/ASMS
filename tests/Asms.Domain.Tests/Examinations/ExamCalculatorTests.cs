using Asms.Domain.Examinations;

namespace Asms.Domain.Tests.Examinations;

public class ExamCalculatorTests
{
    [Fact]
    public void CalculatePercentage_returns_expected_rounded_values()
    {
        Assert.Equal(50.00m, ExamCalculator.CalculatePercentage(50m, 100m));
        Assert.Equal(87.50m, ExamCalculator.CalculatePercentage(17.5m, 20m));
        Assert.Equal(33.33m, ExamCalculator.CalculatePercentage(1m, 3m));
        Assert.Equal(66.67m, ExamCalculator.CalculatePercentage(2m, 3m));
        Assert.Equal(0m, ExamCalculator.CalculatePercentage(0m, 100m));
        Assert.Equal(100m, ExamCalculator.CalculatePercentage(100m, 100m));
    }

    [Fact]
    public void CalculatePercentage_throws_for_invalid_marks()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => ExamCalculator.CalculatePercentage(1m, 0m));
        Assert.Throws<ArgumentOutOfRangeException>(() => ExamCalculator.CalculatePercentage(-1m, 100m));
        Assert.Throws<ArgumentOutOfRangeException>(() => ExamCalculator.CalculatePercentage(101m, 100m));
    }

    [Fact]
    public void IsPass_compares_obtained_marks_with_the_pass_mark()
    {
        Assert.True(ExamCalculator.IsPass(50m, 50m));
        Assert.True(ExamCalculator.IsPass(51m, 50m));
        Assert.False(ExamCalculator.IsPass(49m, 50m));
    }

    [Fact]
    public void IsPass_throws_for_negative_marks()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => ExamCalculator.IsPass(-1m, 0m));
        Assert.Throws<ArgumentOutOfRangeException>(() => ExamCalculator.IsPass(0m, -1m));
    }

    [Fact]
    public void FindGrade_returns_the_matching_grade()
    {
        var items = CreateGradeScaleItems();

        Assert.Equal("A", ExamCalculator.FindGrade(95m, items)?.GradeName);
        Assert.Equal("A", ExamCalculator.FindGrade(90m, items)?.GradeName);
        Assert.Equal("B", ExamCalculator.FindGrade(85m, items)?.GradeName);
        Assert.Equal("F", ExamCalculator.FindGrade(59.99m, items)?.GradeName);
        Assert.Equal("F", ExamCalculator.FindGrade(0m, items)?.GradeName);
        Assert.Equal("A", ExamCalculator.FindGrade(100m, items)?.GradeName);
    }

    [Fact]
    public void FindGrade_returns_null_when_no_items_match()
    {
        Assert.Null(ExamCalculator.FindGrade(50m, Array.Empty<GradeScaleItem>()));
    }

    [Fact]
    public void FindGrade_prefers_the_item_with_the_highest_minimum()
    {
        var items = new[]
        {
            new GradeScaleItem { GradeName = "B", MinPercentage = 80m, MaxPercentage = 100m },
            new GradeScaleItem { GradeName = "A", MinPercentage = 90m, MaxPercentage = 100m }
        };

        Assert.Equal("A", ExamCalculator.FindGrade(95m, items)?.GradeName);
    }

    [Fact]
    public void FindGrade_throws_for_percentages_outside_zero_to_one_hundred()
    {
        var items = CreateGradeScaleItems();

        Assert.Throws<ArgumentOutOfRangeException>(() => ExamCalculator.FindGrade(-1m, items));
        Assert.Throws<ArgumentOutOfRangeException>(() => ExamCalculator.FindGrade(101m, items));
    }

    private static GradeScaleItem[] CreateGradeScaleItems()
    {
        return new[]
        {
            new GradeScaleItem { GradeName = "A", MinPercentage = 90m, MaxPercentage = 100m, IsPass = true },
            new GradeScaleItem { GradeName = "B", MinPercentage = 80m, MaxPercentage = 89.99m, IsPass = true },
            new GradeScaleItem { GradeName = "C", MinPercentage = 60m, MaxPercentage = 79.99m, IsPass = true },
            new GradeScaleItem { GradeName = "F", MinPercentage = 0m, MaxPercentage = 59.99m, IsPass = false }
        };
    }
}
