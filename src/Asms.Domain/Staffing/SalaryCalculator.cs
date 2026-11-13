namespace Asms.Domain.Staffing;

/// <summary>
/// Calculates net salary from basic pay, allowances, and deductions.
/// </summary>
public static class SalaryCalculator
{
    /// <summary>
    /// Calculates salary after deductions.
    /// </summary>
    public static decimal CalculateNet(decimal basic, decimal allowances, decimal deductions)
    {
        if (basic < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(basic));
        }

        if (allowances < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(allowances));
        }

        if (deductions < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(deductions));
        }

        if (deductions > basic && deductions - basic > allowances)
        {
            throw new ArgumentOutOfRangeException(nameof(deductions));
        }

        return basic - deductions + allowances;
    }
}
