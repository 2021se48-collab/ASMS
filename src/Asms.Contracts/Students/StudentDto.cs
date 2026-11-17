namespace Asms.Contracts.Students;

public sealed record StudentDto(
    Guid Id,
    string StudentNumber,
    string FirstName,
    string LastName,
    string FatherName,
    string? GrandfatherName,
    DateOnly DateOfBirth,
    string? Gender,
    string? Province,
    string? District,
    string? Address,
    string? PhoneNumber,
    string? PreviousSchool,
    DateOnly AdmissionDate,
    string Status,
    long Version,
    IReadOnlyList<GuardianLinkDto> Guardians);
