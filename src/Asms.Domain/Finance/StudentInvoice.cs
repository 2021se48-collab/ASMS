using Asms.Domain.Common;
using Asms.Domain.Enums;

namespace Asms.Domain.Finance;

/// <summary>
/// Records a student's invoice; paid amount is calculated from payment records.
/// </summary>
public class StudentInvoice : BaseEntity
{
    public Guid StudentId { get; set; }

    public Guid AcademicYearId { get; set; }

    public string InvoiceNumber { get; set; } = string.Empty;

    public DateOnly IssueDate { get; set; }

    public DateOnly? DueDate { get; set; }

    public decimal Amount { get; set; }

    public decimal DiscountAmount { get; set; }

    public decimal ScholarshipAmount { get; set; }

    public string Currency { get; set; } = "AFN";

    public InvoiceStatus Status { get; set; } = InvoiceStatus.Draft;

    public string? Notes { get; set; }
}
