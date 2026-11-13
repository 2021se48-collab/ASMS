using Asms.Domain.Common;

namespace Asms.Domain.Security;

public class RolePermission : BaseEntity
{
    public Guid RoleId { get; set; }

    public string Permission { get; set; } = string.Empty;
}
