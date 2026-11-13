using Asms.Domain.Common;
using Asms.Domain.Library;

namespace Asms.Domain.Tests.Library;

public class BookCategoryTests
{
    [Fact]
    public void Book_category_implements_ITenantEntity()
    {
        Assert.IsAssignableFrom<ITenantEntity>(new BookCategory());
    }

    [Fact]
    public void New_book_category_has_empty_name()
    {
        Assert.Equal(string.Empty, new BookCategory().Name);
    }
}
