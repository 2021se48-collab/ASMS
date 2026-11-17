namespace Asms.Application.Students;

public interface IStudentNumberGenerator
{
    Task<string> GenerateAsync(
        Guid schoolId,
        string schoolCode,
        int admissionYear,
        CancellationToken ct = default);
}
