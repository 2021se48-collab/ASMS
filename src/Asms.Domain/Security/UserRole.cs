using Asms.Domain.Common;

namespace Asms.Domain.Security;

public class UserRole : BaseEntity
{
    public Guid UserId { get; set; }

    public Guid RoleId { get; set; }
}
