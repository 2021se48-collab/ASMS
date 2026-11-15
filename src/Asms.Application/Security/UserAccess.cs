using System.Collections.Frozen;

namespace Asms.Application.Security;

public sealed record UserAccess(
    bool IsValid,
    IReadOnlyList<string> Roles,
    IReadOnlySet<string> Permissions,
    Guid? AppUserId)
{
    public static UserAccess Denied { get; } = new(
        false,
        Array.Empty<string>(),
        Array.Empty<string>().ToFrozenSet(StringComparer.Ordinal),
        null);

    public bool Has(string permission) =>
        IsValid && Permissions.Contains(permission);
}
