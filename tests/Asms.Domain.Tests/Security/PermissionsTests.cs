using Asms.Domain.Security;

namespace Asms.Domain.Tests.Security;

public class PermissionsTests
{
    [Fact]
    public void All_is_not_empty()
    {
        Assert.NotEmpty(Permissions.All);
    }

    [Fact]
    public void All_has_no_duplicates()
    {
        Assert.Equal(Permissions.All.Count, Permissions.All.Distinct().Count());
    }

    [Fact]
    public void Every_permission_has_exactly_one_dot()
    {
        Assert.All(Permissions.All, permission => Assert.Equal(1, permission.Count(character => character == '.')));
    }

    [Fact]
    public void All_contains_required_permissions()
    {
        Assert.Contains("Students.View", Permissions.All);
        Assert.Contains("Fees.CollectPayment", Permissions.All);
        Assert.Contains("Exams.Finalize", Permissions.All);
        Assert.Contains("Audit.View", Permissions.All);
    }
}
