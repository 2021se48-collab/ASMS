using System.Reflection;
using Asms.Domain.Security;

namespace Asms.Domain.Tests.Security;

public class SystemRolesTests
{
    [Fact]
    public void All_contains_exactly_11_roles()
    {
        Assert.Equal(11, SystemRoles.All.Count);
    }

    [Fact]
    public void All_has_no_duplicate_roles()
    {
        Assert.Equal(SystemRoles.All.Count, SystemRoles.All.Distinct().Count());
    }

    [Fact]
    public void Each_role_constant_name_matches_its_value()
    {
        var roleFields = typeof(SystemRoles).GetFields(BindingFlags.Public | BindingFlags.Static)
            .Where(field => field.IsLiteral && field.FieldType == typeof(string));

        foreach (var field in roleFields)
        {
            Assert.Equal(field.Name, field.GetRawConstantValue());
        }
    }
}
