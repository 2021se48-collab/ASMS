namespace Asms.Contracts.Students;

public sealed record CreateStudentRequest(
    string? FirstName,
    string? LastName,
    string? FatherName,
    string? GrandfatherName,
    DateOnly DateOfBirth,
    string? Gender,
    string? Province,
    string? District,
    string? Address,
    string? PhoneNumber,
    string? PreviousSchool,
    DateOnly AdmissionDate,
    string? Status,
    IReadOnlyList<GuardianLinkDto>? Guardians,
    bool ConfirmDuplicate = false);
