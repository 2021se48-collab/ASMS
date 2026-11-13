using Asms.Domain.Common;
using Asms.Domain.Enums;

namespace Asms.Domain.Staffing;

/// <summary>
/// A paid salary record must not be changed; the Application layer enforces this rule.
/// </summary>
public class SalaryRecord : BaseEntity
{
    public Guid EmployeeId { get; set; }

    public EmployeeType EmployeeType { get; set; }

    public DateOnly PeriodStart { get; set; }

    public DateOnly PeriodEnd { get; set; }

    public decimal BasicSalary { get; set; }

    public decimal Allowances { get; set; }

    public decimal Deductions { get; set; }

    public decimal NetSalary { get; set; }

    public string Currency { get; set; } = "AFN";

    public SalaryStatus Status { get; set; } = SalaryStatus.Draft;

    public DateTime? PaidAtUtc { get; set; }

    public string? Notes { get; set; }
}
