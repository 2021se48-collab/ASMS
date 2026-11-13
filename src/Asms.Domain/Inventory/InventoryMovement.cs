using Asms.Domain.Common;
using Asms.Domain.Enums;

namespace Asms.Domain.Inventory;

/// <summary>
/// Movements are history and are never edited or deleted; damage and loss are recorded, not hidden.
/// </summary>
public class InventoryMovement : BaseEntity
{
    public Guid InventoryItemId { get; init; }

    public InventoryMovementType MovementType { get; init; }

    public int Quantity { get; init; }

    public string? Reason { get; init; }

    public DateTime OccurredAtUtc { get; init; }

    public Guid? RecordedByUserId { get; init; }

    /// <summary>
    /// Creates an inventory movement with a positive quantity.
    /// </summary>
    public static InventoryMovement Create(
        Guid schoolId,
        Guid itemId,
        InventoryMovementType type,
        int quantity,
        string? reason,
        DateTime occurredAtUtc,
        Guid? userId)
    {
        if (quantity < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(quantity));
        }

        return new InventoryMovement
        {
            SchoolId = schoolId,
            InventoryItemId = itemId,
            MovementType = type,
            Quantity = quantity,
            Reason = reason,
            OccurredAtUtc = occurredAtUtc,
            RecordedByUserId = userId
        };
    }
}
