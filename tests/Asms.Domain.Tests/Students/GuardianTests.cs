using Asms.Domain.Common;
using Asms.Domain.Students;

namespace Asms.Domain.Tests.Students;

public class GuardianTests
{
    [Fact]
    public void Guardian_implements_ITenantEntity()
    {
        Assert.IsAssignableFrom<ITenantEntity>(new Guardian());
    }

    [Fact]
    public void New_guardian_has_an_empty_FullName()
    {
        Assert.Equal(string.Empty, new Guardian().FullName);
    }
}
