using Asms.Domain.Enums;

namespace Asms.Domain.Tests.Enums;

public class FinanceEnumTests
{
    [Fact]
    public void Finance_enum_values_match_database_values()
    {
        Assert.Equal(0, (int)InvoiceStatus.Draft);
        Assert.Equal(1, (int)InvoiceStatus.Issued);
        Assert.Equal(2, (int)InvoiceStatus.PartiallyPaid);
        Assert.Equal(3, (int)InvoiceStatus.Paid);
        Assert.Equal(4, (int)InvoiceStatus.Cancelled);

        Assert.Equal(0, (int)PaymentKind.Payment);
        Assert.Equal(1, (int)PaymentKind.Reversal);

        Assert.Equal(0, (int)PaymentMethod.Cash);
        Assert.Equal(1, (int)PaymentMethod.BankTransfer);
        Assert.Equal(2, (int)PaymentMethod.MobileMoney);
        Assert.Equal(3, (int)PaymentMethod.Other);
    }
}
