using Asms.Domain.Common;
using Asms.Domain.Enums;

namespace Asms.Domain.Staffing;

/// <summary>
/// Stores staff names exactly as typed and never translated.
/// </summary>
public class StaffMember : BaseEntity
{
    public string EmployeeNumber { get; set; } = string.Empty;

    public string FirstName { get; set; } = string.Empty;

    public string LastName { get; set; } = string.Empty;

    public string? FatherName { get; set; }

    public Gender? Gender { get; set; }

    public DateOnly? DateOfBirth { get; set; }

    public string? PhoneNumber { get; set; }

    public string? Email { get; set; }

    public string? Province { get; set; }

    public string? District { get; set; }

    public string? Address { get; set; }

    public string? JobTitle { get; set; }

    public string? Department { get; set; }

    public DateOnly HireDate { get; set; }

    public EmploymentStatus Status { get; set; } = EmploymentStatus.Active;

    public Guid? AppUserId { get; set; }

    public string? PhotoPath { get; set; }
}
