using Asms.Domain.Common;

namespace Asms.Domain.Academics;

/// <summary>
/// Represents an academic year.
/// </summary>
public class AcademicYear : BaseEntity
{
    public string Name { get; set; } = string.Empty;

    public DateOnly StartDate { get; set; }

    public DateOnly EndDate { get; set; }

    public bool IsCurrent { get; set; }

    public bool IsClosed { get; set; }

    public bool HasValidDateRange() => EndDate > StartDate;

    public bool Contains(DateOnly date) => date >= StartDate && date <= EndDate;
}
