using Asms.Domain.Common;

namespace Asms.Domain.Finance;

/// <summary>
/// Records a school expense.
/// </summary>
public class Expense : BaseEntity
{
    public string Title { get; set; } = string.Empty;

    public string? Category { get; set; }

    public decimal Amount { get; set; }

    public string Currency { get; set; } = "AFN";

    public DateOnly ExpenseDate { get; set; }

    public string? PaidTo { get; set; }

    public string? Notes { get; set; }
}
