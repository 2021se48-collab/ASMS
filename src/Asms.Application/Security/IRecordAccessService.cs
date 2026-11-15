namespace Asms.Application.Security;

/// <summary>
/// Limits student access to a user's school role, teaching assignments, or linked children.
/// Only school leaders can manage any class; teachers can manage assigned classes.
/// </summary>
public interface IRecordAccessService
{
    /// <summary>
    /// Checks whether the current user may access one student in the current school.
    /// </summary>
    Task<bool> CanAccessStudentAsync(Guid studentId, CancellationToken ct = default);

    /// <summary>
    /// Gets student IDs visible to the current user in the current school.
    /// </summary>
    Task<IReadOnlySet<Guid>> GetAccessibleStudentIdsAsync(CancellationToken ct = default);

    /// <summary>
    /// Checks whether the current user may manage the class and optional section or subject.
    /// </summary>
    Task<bool> CanManageClassAsync(
        Guid classRoomId,
        Guid? sectionId,
        Guid? subjectId,
        CancellationToken ct = default);
}
