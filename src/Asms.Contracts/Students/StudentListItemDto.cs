namespace Asms.Contracts.Students;

public sealed record StudentListItemDto(
    Guid Id,
    string StudentNumber,
    string FirstName,
    string LastName,
    string FatherName,
    DateOnly DateOfBirth,
    DateOnly AdmissionDate,
    string Status,
    long Version);
