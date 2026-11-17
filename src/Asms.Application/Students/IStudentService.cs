using Asms.Contracts.Students;

namespace Asms.Application.Students;

public interface IStudentService
{
    Task<StudentResult<StudentDto>> CreateAsync(
        CreateStudentRequest request,
        CancellationToken ct = default);

    Task<StudentResult<StudentDto>> UpdateAsync(
        Guid studentId,
        UpdateStudentRequest request,
        CancellationToken ct = default);

    Task<StudentResult<StudentDto>> GetAsync(
        Guid studentId,
        CancellationToken ct = default);

    Task<StudentResult<PagedResult<StudentListItemDto>>> SearchAsync(
        StudentSearchQuery query,
        CancellationToken ct = default);
}
