using Asms.Domain.Common;
using Asms.Domain.Enums;

namespace Asms.Domain.Students;

/// <summary>
/// Stores student names exactly as typed; names are never translated.
/// </summary>
public class Student : BaseEntity
{
    public string StudentNumber { get; set; } = string.Empty;

    public string FirstName { get; set; } = string.Empty;

    public string LastName { get; set; } = string.Empty;

    public string FatherName { get; set; } = string.Empty;

    public string? GrandfatherName { get; set; }

    public DateOnly DateOfBirth { get; set; }

    public Gender? Gender { get; set; }

    public string? Province { get; set; }

    public string? District { get; set; }

    public string? Address { get; set; }

    public string? PhoneNumber { get; set; }

    public string? PreviousSchool { get; set; }

    public DateOnly AdmissionDate { get; set; }

    public StudentStatus Status { get; set; } = StudentStatus.Active;

    public string? PhotoPath { get; set; }

    public string DuplicateKey { get; set; } = string.Empty;

    public void RefreshDuplicateKey()
    {
        DuplicateKey = StudentDuplicateKey.Create(FirstName, FatherName, DateOfBirth);
    }
}
