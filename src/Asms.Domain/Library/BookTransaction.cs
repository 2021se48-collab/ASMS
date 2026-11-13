using Asms.Domain.Common;
using Asms.Domain.Enums;

namespace Asms.Domain.Library;

/// <summary>
/// Tracks a book loan; overdue status is calculated and never stored.
/// </summary>
public class BookTransaction : BaseEntity
{
    public Guid BookId { get; set; }

    public Guid? StudentId { get; set; }

    public Guid? TeacherId { get; set; }

    public DateOnly IssueDate { get; set; }

    public DateOnly DueDate { get; set; }

    public DateOnly? ReturnDate { get; set; }

    public BookTransactionStatus Status { get; set; } = BookTransactionStatus.Issued;

    public string? Notes { get; set; }

    public bool IsOverdue(DateOnly today) =>
        Status == BookTransactionStatus.Issued && ReturnDate == null && today > DueDate;

    public int DaysOverdue(DateOnly today) =>
        IsOverdue(today) ? today.DayNumber - DueDate.DayNumber : 0;
}
