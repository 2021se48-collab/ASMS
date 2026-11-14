using System.Collections.Frozen;

namespace Asms.Application.Security;

public sealed record UserAccess(
    bool IsValid,
    IReadOnlyList<string> Roles,
    IReadOnlySet<string> Permissions)
{
    public static UserAccess Denied { get; } = new(
        false,
        Array.Empty<string>(),
        Array.Empty<string>().ToFrozenSet(StringComparer.Ordinal));

    public bool Has(string permission) =>
        IsValid && Permissions.Contains(permission);
}
