namespace Asms.Application.Abstractions;

/// <summary>
/// Tells the database which school, user, and device are making the request.
/// </summary>
public interface ICurrentTenant
{
    Guid? SchoolId { get; }

    Guid? UserId { get; }

    Guid? DeviceId { get; }
}
