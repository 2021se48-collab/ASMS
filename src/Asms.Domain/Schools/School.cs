namespace Asms.Domain.Schools;

/// <summary>
/// A school is the tenant that owns data, so it has no SchoolId of its own.
/// </summary>
public class School
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public string Name { get; set; } = string.Empty;

    public string? Code { get; set; }

    public string? Province { get; set; }

    public string? District { get; set; }

    public string? Address { get; set; }

    public string? PhoneNumber { get; set; }

    public string? Email { get; set; }

    public string DefaultLanguage { get; set; } = "en";

    public string CalendarType { get; set; } = "SolarHijri";

    public bool IsActive { get; set; } = true;

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

    public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;

    public long Version { get; set; } = 1;

    public bool IsDeleted { get; set; }

    public Guid? LastModifiedByDeviceId { get; set; }
}
