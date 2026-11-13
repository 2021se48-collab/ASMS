using Asms.Domain.Common;
using Asms.Domain.Finance;
using Asms.Domain.Enums;

namespace Asms.Domain.Tests.Finance;

public class FinanceEntitiesTests
{
    [Fact]
    public void Finance_entities_implement_ITenantEntity()
    {
        Assert.IsAssignableFrom<ITenantEntity>(new FeeCategory());
        Assert.IsAssignableFrom<ITenantEntity>(new FeeStructure());
        Assert.IsAssignableFrom<ITenantEntity>(new StudentInvoice());
        Assert.IsAssignableFrom<ITenantEntity>(new Payment());
        Assert.IsAssignableFrom<ITenantEntity>(new Expense());
    }

    [Fact]
    public void Finance_entities_have_expected_defaults()
    {
        var feeCategory = new FeeCategory();
        var feeStructure = new FeeStructure();
        var invoice = new StudentInvoice();
        var payment = new Payment();
        var expense = new Expense();

        Assert.True(feeCategory.IsActive);
        Assert.Equal(string.Empty, feeCategory.Name);
        Assert.Equal(string.Empty, invoice.InvoiceNumber);
        Assert.Equal(string.Empty, payment.ReceiptNumber);
        Assert.Equal(string.Empty, expense.Title);

        Assert.Equal("AFN", feeStructure.Currency);
        Assert.Equal("AFN", invoice.Currency);
        Assert.Equal("AFN", payment.Currency);
        Assert.Equal("AFN", expense.Currency);

        Assert.Equal(InvoiceStatus.Draft, invoice.Status);
        Assert.Equal(0m, invoice.DiscountAmount);
        Assert.Equal(0m, invoice.ScholarshipAmount);
    }
}
