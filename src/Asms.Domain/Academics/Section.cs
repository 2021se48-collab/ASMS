using Asms.Domain.Common;

namespace Asms.Domain.Academics;

/// <summary>
/// Represents a section in a classroom.
/// </summary>
public class Section : BaseEntity
{
    public Guid ClassRoomId { get; set; }

    public string Name { get; set; } = string.Empty;

    public int? Capacity { get; set; }
}
