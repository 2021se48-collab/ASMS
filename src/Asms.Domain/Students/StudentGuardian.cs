using Asms.Domain.Common;
using Asms.Domain.Enums;

namespace Asms.Domain.Students;

/// <summary>
/// Links a guardian to a student; parents can only see students linked here.
/// </summary>
public class StudentGuardian : BaseEntity
{
    public Guid StudentId { get; set; }

    public Guid GuardianId { get; set; }

    public GuardianRelationship Relationship { get; set; } = GuardianRelationship.Other;

    public bool IsPrimaryContact { get; set; }

    public bool IsEmergencyContact { get; set; }
}
