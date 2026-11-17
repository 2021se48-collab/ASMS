namespace Asms.Contracts.Students;

public sealed record GuardianLinkDto(
    Guid? GuardianId,
    string? FullName,
    string? PhoneNumber,
    string? Email,
    string Relationship,
    bool IsPrimaryContact,
    bool IsEmergencyContact);
