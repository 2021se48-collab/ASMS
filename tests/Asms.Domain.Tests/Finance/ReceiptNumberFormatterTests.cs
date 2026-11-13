using Asms.Domain.Finance;

namespace Asms.Domain.Tests.Finance;

public class ReceiptNumberFormatterTests
{
    [Fact]
    public void Format_returns_the_expected_receipt_number()
    {
        Assert.Equal(
            "DEV01-20261113-0007",
            ReceiptNumberFormatter.Format("dev01", new DateOnly(2026, 11, 13), 7));
    }

    [Fact]
    public void Format_uses_the_device_code_to_make_numbers_different()
    {
        var date = new DateOnly(2026, 11, 13);

        Assert.NotEqual(
            ReceiptNumberFormatter.Format("dev01", date, 7),
            ReceiptNumberFormatter.Format("dev02", date, 7));
    }

    [Fact]
    public void Format_throws_for_blank_or_invalid_device_codes()
    {
        var date = new DateOnly(2026, 11, 13);

        Assert.Throws<ArgumentException>(() => ReceiptNumberFormatter.Format(null!, date, 1));
        Assert.Throws<ArgumentException>(() => ReceiptNumberFormatter.Format(" ", date, 1));
        Assert.Throws<ArgumentException>(() => ReceiptNumberFormatter.Format("dev-01", date, 1));
        Assert.Throws<ArgumentException>(() => ReceiptNumberFormatter.Format("dev 01", date, 1));
    }

    [Fact]
    public void Format_throws_for_non_positive_sequence()
    {
        var date = new DateOnly(2026, 11, 13);

        Assert.Throws<ArgumentOutOfRangeException>(() => ReceiptNumberFormatter.Format("dev01", date, 0));
        Assert.Throws<ArgumentOutOfRangeException>(() => ReceiptNumberFormatter.Format("dev01", date, -1));
    }
}
