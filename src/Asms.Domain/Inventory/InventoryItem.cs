using Asms.Domain.Common;
using Asms.Domain.Enums;

namespace Asms.Domain.Inventory;

/// <summary>
/// Represents an item owned by a school.
/// </summary>
public class InventoryItem : BaseEntity
{
    public string Name { get; set; } = string.Empty;

    public string? Category { get; set; }

    public string? Code { get; set; }

    public int Quantity { get; set; }

    public InventoryCondition Condition { get; set; } = InventoryCondition.Good;

    public string? Location { get; set; }

    public DateOnly? PurchaseDate { get; set; }

    public decimal? UnitCost { get; set; }

    public string Currency { get; set; } = "AFN";
}
