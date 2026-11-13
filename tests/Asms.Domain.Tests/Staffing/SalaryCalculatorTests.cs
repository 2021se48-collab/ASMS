using Asms.Domain.Staffing;

namespace Asms.Domain.Tests.Staffing;

public class SalaryCalculatorTests
{
    [Fact]
    public void CalculateNet_returns_expected_values()
    {
        Assert.Equal(11000m, SalaryCalculator.CalculateNet(10000m, 1500m, 500m));
        Assert.Equal(10000m, SalaryCalculator.CalculateNet(10000m, 0m, 0m));
        Assert.Equal(0m, SalaryCalculator.CalculateNet(100m, 50m, 150m));
        Assert.Equal(1000.00m, SalaryCalculator.CalculateNet(1000.50m, 0.25m, 0.75m));
    }

    [Fact]
    public void CalculateNet_throws_when_deductions_exceed_gross_salary()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            SalaryCalculator.CalculateNet(100m, 50m, 151m));
    }

    [Fact]
    public void CalculateNet_throws_for_each_negative_input()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => SalaryCalculator.CalculateNet(-1m, 0m, 0m));
        Assert.Throws<ArgumentOutOfRangeException>(() => SalaryCalculator.CalculateNet(0m, -1m, 0m));
        Assert.Throws<ArgumentOutOfRangeException>(() => SalaryCalculator.CalculateNet(0m, 0m, -1m));
    }
}
