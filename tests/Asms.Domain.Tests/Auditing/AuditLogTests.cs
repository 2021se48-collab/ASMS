using System.Runtime.CompilerServices;
using Asms.Domain.Auditing;
using Asms.Domain.Common;

namespace Asms.Domain.Tests.Auditing;

public class AuditLogTests
{
    [Fact]
    public void Audit_log_implements_ITenantEntity()
    {
        Assert.IsAssignableFrom<ITenantEntity>(new AuditLog());
    }

    [Fact]
    public void Audit_properties_are_init_only()
    {
        var propertyNames = new[]
        {
            nameof(AuditLog.UserId),
            nameof(AuditLog.Action),
            nameof(AuditLog.EntityType),
            nameof(AuditLog.EntityId),
            nameof(AuditLog.OldValuesJson),
            nameof(AuditLog.NewValuesJson),
            nameof(AuditLog.IpAddress),
            nameof(AuditLog.DeviceId),
            nameof(AuditLog.OccurredAtUtc)
        };

        foreach (var propertyName in propertyNames)
        {
            var property = typeof(AuditLog).GetProperty(propertyName);
            Assert.NotNull(property);
            Assert.NotNull(property!.SetMethod);
            Assert.Contains(
                typeof(IsExternalInit),
                property.SetMethod!.ReturnParameter.GetRequiredCustomModifiers());
        }
    }

    [Fact]
    public void Audit_string_properties_default_to_empty_strings()
    {
        var auditLog = new AuditLog();

        Assert.Equal(string.Empty, auditLog.Action);
        Assert.Equal(string.Empty, auditLog.EntityType);
    }
}
