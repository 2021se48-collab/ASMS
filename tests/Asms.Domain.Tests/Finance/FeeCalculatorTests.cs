using Asms.Domain.Enums;
using Asms.Domain.Finance;

namespace Asms.Domain.Tests.Finance;

public class FeeCalculatorTests
{
    [Fact]
    public void CalculateNetAmount_returns_expected_values()
    {
        Assert.Equal(700m, FeeCalculator.CalculateNetAmount(1000m, 100m, 200m));
        Assert.Equal(1000m, FeeCalculator.CalculateNetAmount(1000m, 0m, 0m));
        Assert.Equal(0m, FeeCalculator.CalculateNetAmount(1000m, 400m, 600m));
    }

    [Fact]
    public void CalculateNetAmount_throws_when_deductions_exceed_amount()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            FeeCalculator.CalculateNetAmount(100m, 60m, 50m));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            FeeCalculator.CalculateNetAmount(decimal.MaxValue, decimal.MaxValue, decimal.MaxValue));
    }

    [Fact]
    public void CalculateNetAmount_throws_for_negative_values()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => FeeCalculator.CalculateNetAmount(-1m, 0m, 0m));
        Assert.Throws<ArgumentOutOfRangeException>(() => FeeCalculator.CalculateNetAmount(1m, -1m, 0m));
        Assert.Throws<ArgumentOutOfRangeException>(() => FeeCalculator.CalculateNetAmount(1m, 0m, -1m));
    }

    [Fact]
    public void CalculatePaidTotal_subtracts_reversals_from_payments()
    {
        var payments = new[]
        {
            new Payment { Amount = 100m, Kind = PaymentKind.Payment },
            new Payment { Amount = 50m, Kind = PaymentKind.Payment },
            new Payment { Amount = 20m, Kind = PaymentKind.Reversal }
        };

        Assert.Equal(130m, FeeCalculator.CalculatePaidTotal(payments));
    }

    [Fact]
    public void CalculatePaidTotal_returns_zero_for_an_empty_list()
    {
        Assert.Equal(0m, FeeCalculator.CalculatePaidTotal(Array.Empty<Payment>()));
    }

    [Fact]
    public void CalculatePaidTotal_throws_for_null()
    {
        Assert.Throws<ArgumentNullException>(() => FeeCalculator.CalculatePaidTotal(null!));
    }

    [Fact]
    public void CalculateBalance_returns_partial_exact_and_negative_overpayment_balances()
    {
        Assert.Equal(60m, FeeCalculator.CalculateBalance(100m, new[]
        {
            new Payment { Amount = 40m, Kind = PaymentKind.Payment }
        }));
        Assert.Equal(0m, FeeCalculator.CalculateBalance(100m, new[]
        {
            new Payment { Amount = 100m, Kind = PaymentKind.Payment }
        }));
        Assert.Equal(-20m, FeeCalculator.CalculateBalance(100m, new[]
        {
            new Payment { Amount = 120m, Kind = PaymentKind.Payment }
        }));
    }

    [Fact]
    public void DetermineStatus_returns_the_expected_invoice_status()
    {
        Assert.Equal(InvoiceStatus.Cancelled, FeeCalculator.DetermineStatus(100m, 0m, true));
        Assert.Equal(InvoiceStatus.Paid, FeeCalculator.DetermineStatus(100m, 100m, false));
        Assert.Equal(InvoiceStatus.PartiallyPaid, FeeCalculator.DetermineStatus(100m, 1m, false));
        Assert.Equal(InvoiceStatus.Issued, FeeCalculator.DetermineStatus(100m, 0m, false));
        Assert.Equal(InvoiceStatus.Issued, FeeCalculator.DetermineStatus(0m, 0m, false));
    }
}
