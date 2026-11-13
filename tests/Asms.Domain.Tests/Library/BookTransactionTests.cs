using Asms.Domain.Common;
using Asms.Domain.Enums;
using Asms.Domain.Library;

namespace Asms.Domain.Tests.Library;

public class BookTransactionTests
{
    [Fact]
    public void Book_transaction_implements_ITenantEntity()
    {
        Assert.IsAssignableFrom<ITenantEntity>(new BookTransaction());
    }

    [Fact]
    public void New_book_transaction_has_expected_defaults()
    {
        var transaction = new BookTransaction();

        Assert.Equal(BookTransactionStatus.Issued, transaction.Status);
        Assert.Null(transaction.ReturnDate);
    }

    [Fact]
    public void IsOverdue_is_false_on_due_date_and_true_after_due_date()
    {
        var dueDate = new DateOnly(2026, 11, 13);
        var transaction = new BookTransaction { DueDate = dueDate };

        Assert.False(transaction.IsOverdue(dueDate));
        Assert.True(transaction.IsOverdue(dueDate.AddDays(1)));
    }

    [Fact]
    public void IsOverdue_is_false_when_returned_or_status_is_not_issued()
    {
        var today = new DateOnly(2026, 11, 14);
        var returned = new BookTransaction
        {
            DueDate = today.AddDays(-1),
            ReturnDate = today
        };
        var notIssued = new BookTransaction
        {
            DueDate = today.AddDays(-1),
            Status = BookTransactionStatus.Returned
        };

        Assert.False(returned.IsOverdue(today));
        Assert.False(notIssued.IsOverdue(today));
    }

    [Fact]
    public void DaysOverdue_returns_zero_when_not_overdue_and_counts_late_days()
    {
        var dueDate = new DateOnly(2026, 11, 13);
        var transaction = new BookTransaction { DueDate = dueDate };

        Assert.Equal(0, transaction.DaysOverdue(dueDate));
        Assert.Equal(5, transaction.DaysOverdue(dueDate.AddDays(5)));
    }
}
