using Asms.Domain.Common;
using Asms.Domain.Security;

namespace Asms.Domain.Tests.Security;

public class IdentityEntitiesTests
{
    [Fact]
    public void Identity_entities_implement_ITenantEntity()
    {
        Assert.IsAssignableFrom<ITenantEntity>(new AppUser());
        Assert.IsAssignableFrom<ITenantEntity>(new AppRole());
        Assert.IsAssignableFrom<ITenantEntity>(new UserRole());
        Assert.IsAssignableFrom<ITenantEntity>(new RolePermission());
    }

    [Fact]
    public void New_app_user_has_active_and_language_defaults()
    {
        var user = new AppUser();

        Assert.True(user.IsActive);
        Assert.Equal("en", user.PreferredLanguage);
    }

    [Fact]
    public void New_app_role_is_not_a_system_role()
    {
        var role = new AppRole();

        Assert.False(role.IsSystemRole);
    }

    [Fact]
    public void Required_string_properties_default_to_empty_strings()
    {
        Assert.Equal(string.Empty, new AppUser().FullName);
        Assert.Equal(string.Empty, new AppRole().Name);
        Assert.Equal(string.Empty, new RolePermission().Permission);
    }
}
