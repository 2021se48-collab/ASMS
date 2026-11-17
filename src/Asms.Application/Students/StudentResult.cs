using Asms.Contracts.Students;

namespace Asms.Application.Students;

public sealed record StudentResult<T>(
    bool Succeeded,
    T? Value,
    string? ErrorCode,
    IReadOnlyList<DuplicateMatchDto> Duplicates)
{
    public static StudentResult<T> Success(T value) =>
        new(true, value, null, Array.Empty<DuplicateMatchDto>());

    public static StudentResult<T> Failure(
        string errorCode,
        IReadOnlyList<DuplicateMatchDto>? duplicates = null) =>
        new(false, default, errorCode, duplicates ?? Array.Empty<DuplicateMatchDto>());
}
