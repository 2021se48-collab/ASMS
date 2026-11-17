namespace Asms.Contracts.Students;

public sealed record DuplicateMatchDto(
    Guid Id,
    string StudentNumber,
    string FirstName,
    string LastName,
    string FatherName,
    DateOnly DateOfBirth);
