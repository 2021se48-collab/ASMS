namespace Asms.Domain.Examinations;

/// <summary>
/// Calculates marks using school grading data; grading rules are never hardcoded.
/// </summary>
public static class ExamCalculator
{
    /// <summary>
    /// Calculates a percentage rounded to two decimal places.
    /// </summary>
    public static decimal CalculatePercentage(decimal obtained, decimal max)
    {
        if (max <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(max));
        }

        if (obtained < 0 || obtained > max)
        {
            throw new ArgumentOutOfRangeException(nameof(obtained));
        }

        return Math.Round(obtained / max * 100m, 2, MidpointRounding.AwayFromZero);
    }

    /// <summary>
    /// Checks whether obtained marks meet the pass mark.
    /// </summary>
    public static bool IsPass(decimal obtained, decimal passMarks)
    {
        if (obtained < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(obtained));
        }

        if (passMarks < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(passMarks));
        }

        return obtained >= passMarks;
    }

    /// <summary>
    /// Finds the matching grade, preferring the highest minimum percentage.
    /// </summary>
    public static GradeScaleItem? FindGrade(decimal percentage, IEnumerable<GradeScaleItem> items)
    {
        if (percentage is < 0 or > 100)
        {
            throw new ArgumentOutOfRangeException(nameof(percentage));
        }

        return items
            .Where(item => item.MinPercentage <= percentage && percentage <= item.MaxPercentage)
            .OrderByDescending(item => item.MinPercentage)
            .FirstOrDefault();
    }
}
