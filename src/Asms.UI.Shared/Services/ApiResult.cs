namespace Asms.UI.Shared.Services;

public sealed record ApiResult<T>(
    bool Succeeded,
    T? Value,
    string? ErrorCode,
    bool IsNetworkError)
{
    public static ApiResult<T> Success(T value) => new(true, value, null, false);

    public static ApiResult<T> Failure(string errorCode, bool isNetworkError = false) =>
        new(false, default, errorCode, isNetworkError);
}
