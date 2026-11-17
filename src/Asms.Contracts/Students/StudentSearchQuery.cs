namespace Asms.Contracts.Students;

public sealed record StudentSearchQuery(
    string? Text = null,
    string? Status = null,
    int Page = 1,
    int PageSize = 20);
