using System.Runtime.CompilerServices;
using Asms.Domain.Common;
using Asms.Domain.Enums;
using Asms.Domain.Inventory;

namespace Asms.Domain.Tests.Inventory;

public class InventoryTests
{
    [Fact]
    public void Inventory_entities_implement_ITenantEntity()
    {
        Assert.IsAssignableFrom<ITenantEntity>(new InventoryItem());
        Assert.IsAssignableFrom<ITenantEntity>(new InventoryMovement());
    }

    [Fact]
    public void New_inventory_item_has_expected_defaults()
    {
        var item = new InventoryItem();

        Assert.Equal(InventoryCondition.Good, item.Condition);
        Assert.Equal(0, item.Quantity);
        Assert.Equal("AFN", item.Currency);
    }

    [Fact]
    public void Create_sets_all_movement_fields_and_generates_an_id()
    {
        var schoolId = Guid.NewGuid();
        var itemId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var occurredAtUtc = new DateTime(2026, 11, 13, 9, 0, 0, DateTimeKind.Utc);

        var movement = InventoryMovement.Create(
            schoolId,
            itemId,
            InventoryMovementType.StockIn,
            4,
            "Received",
            occurredAtUtc,
            userId);

        Assert.NotEqual(Guid.Empty, movement.Id);
        Assert.Equal(schoolId, movement.SchoolId);
        Assert.Equal(itemId, movement.InventoryItemId);
        Assert.Equal(InventoryMovementType.StockIn, movement.MovementType);
        Assert.Equal(4, movement.Quantity);
        Assert.Equal("Received", movement.Reason);
        Assert.Equal(occurredAtUtc, movement.OccurredAtUtc);
        Assert.Equal(userId, movement.RecordedByUserId);
    }

    [Fact]
    public void Create_throws_for_non_positive_quantity()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => CreateMovement(0));
        Assert.Throws<ArgumentOutOfRangeException>(() => CreateMovement(-1));
    }

    [Fact]
    public void Movement_properties_are_init_only()
    {
        var propertyNames = new[]
        {
            nameof(InventoryMovement.InventoryItemId),
            nameof(InventoryMovement.MovementType),
            nameof(InventoryMovement.Quantity),
            nameof(InventoryMovement.Reason),
            nameof(InventoryMovement.OccurredAtUtc),
            nameof(InventoryMovement.RecordedByUserId)
        };

        foreach (var propertyName in propertyNames)
        {
            var property = typeof(InventoryMovement).GetProperty(propertyName);
            Assert.NotNull(property);
            Assert.NotNull(property!.SetMethod);
            Assert.Contains(
                typeof(IsExternalInit),
                property.SetMethod!.ReturnParameter.GetRequiredCustomModifiers());
        }
    }

    private static InventoryMovement CreateMovement(int quantity)
    {
        return InventoryMovement.Create(
            Guid.NewGuid(),
            Guid.NewGuid(),
            InventoryMovementType.StockIn,
            quantity,
            null,
            DateTime.UtcNow,
            null);
    }
}
