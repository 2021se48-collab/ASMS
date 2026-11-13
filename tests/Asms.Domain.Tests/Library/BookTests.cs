using Asms.Domain.Common;
using Asms.Domain.Library;

namespace Asms.Domain.Tests.Library;

public class BookTests
{
    [Fact]
    public void Book_implements_ITenantEntity()
    {
        Assert.IsAssignableFrom<ITenantEntity>(new Book());
    }

    [Fact]
    public void New_book_has_empty_title_and_no_copies()
    {
        var book = new Book();

        Assert.Equal(string.Empty, book.Title);
        Assert.Equal(0, book.TotalCopies);
        Assert.Equal(0, book.AvailableCopies);
    }

    [Fact]
    public void AddCopies_increases_total_and_available_copies()
    {
        var book = new Book();

        book.AddCopies(5);

        Assert.Equal(5, book.TotalCopies);
        Assert.Equal(5, book.AvailableCopies);
    }

    [Fact]
    public void AddCopies_throws_for_counts_below_one()
    {
        var book = new Book();

        Assert.Throws<ArgumentOutOfRangeException>(() => book.AddCopies(0));
        Assert.Throws<ArgumentOutOfRangeException>(() => book.AddCopies(-1));
    }

    [Fact]
    public void Checkout_decreases_available_copies()
    {
        var book = new Book();
        book.AddCopies(2);

        book.Checkout();

        Assert.Equal(1, book.AvailableCopies);
        Assert.Equal(2, book.TotalCopies);
    }

    [Fact]
    public void Checkout_throws_when_no_copies_are_available()
    {
        Assert.Throws<InvalidOperationException>(() => new Book().Checkout());
    }

    [Fact]
    public void Return_increases_available_copies()
    {
        var book = new Book();
        book.AddCopies(2);
        book.Checkout();

        book.Return();

        Assert.Equal(2, book.AvailableCopies);
    }

    [Fact]
    public void Return_throws_when_all_copies_are_already_available()
    {
        var book = new Book();
        book.AddCopies(1);

        Assert.Throws<InvalidOperationException>(() => book.Return());
    }

    [Fact]
    public void Checkout_then_return_restores_available_copies()
    {
        var book = new Book();
        book.AddCopies(3);

        book.Checkout();
        book.Return();

        Assert.Equal(3, book.AvailableCopies);
    }
}
