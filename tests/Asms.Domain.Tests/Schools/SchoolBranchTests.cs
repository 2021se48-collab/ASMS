using Asms.Domain.Common;
using Asms.Domain.Schools;

namespace Asms.Domain.Tests.Schools;

public class SchoolBranchTests
{
    [Fact]
    public void School_branch_implements_ITenantEntity()
    {
        var branch = new SchoolBranch();

        Assert.IsAssignableFrom<ITenantEntity>(branch);
    }

    [Fact]
    public void New_school_branch_has_empty_SchoolId_until_assigned()
    {
        var branch = new SchoolBranch();

        Assert.Equal(Guid.Empty, branch.SchoolId);
    }

    [Fact]
    public void New_school_branch_is_active()
    {
        var branch = new SchoolBranch();

        Assert.True(branch.IsActive);
    }

    [Fact]
    public void New_school_branch_is_not_the_main_branch()
    {
        var branch = new SchoolBranch();

        Assert.False(branch.IsMainBranch);
    }

    [Fact]
    public void New_school_branch_starts_at_Version_1()
    {
        var branch = new SchoolBranch();

        Assert.Equal(1, branch.Version);
    }

    [Fact]
    public void New_school_branch_has_an_empty_Name()
    {
        var branch = new SchoolBranch();

        Assert.Equal(string.Empty, branch.Name);
    }
}
