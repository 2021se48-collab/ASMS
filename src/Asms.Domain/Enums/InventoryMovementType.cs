namespace Asms.Domain.Enums;

public enum InventoryMovementType
{
    StockIn = 0,
    StockOut = 1,
    MarkedDamaged = 2,
    MarkedMissing = 3,
    SentForMaintenance = 4,
    ReturnedFromMaintenance = 5
}
