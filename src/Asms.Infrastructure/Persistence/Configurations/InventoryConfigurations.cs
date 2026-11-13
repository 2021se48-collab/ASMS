using Asms.Domain.Inventory;
using Asms.Domain.Schools;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Asms.Infrastructure.Persistence.Configurations;

public sealed class InventoryItemConfiguration : IEntityTypeConfiguration<InventoryItem>
{
    public void Configure(EntityTypeBuilder<InventoryItem> builder)
    {
        builder.Property(item => item.Name).IsRequired().HasMaxLength(200);
        builder.Property(item => item.Category).HasMaxLength(200);
        builder.Property(item => item.Code).HasMaxLength(50);
        builder.Property(item => item.Location).HasMaxLength(200);
        builder.Property(item => item.UnitCost).HasPrecision(18, 2);
        builder.Property(item => item.Currency).IsRequired().HasMaxLength(3);
        builder.HasIndex(item => item.SchoolId)
            .HasDatabaseName("IX_InventoryItems_SchoolId");
        builder.HasIndex(item => new { item.SchoolId, item.Code })
            .IsUnique()
            .HasDatabaseName("UX_InventoryItems_SchoolId_Code");
        builder.HasOne<School>().WithMany().HasForeignKey(item => item.SchoolId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class InventoryMovementConfiguration : IEntityTypeConfiguration<InventoryMovement>
{
    public void Configure(EntityTypeBuilder<InventoryMovement> builder)
    {
        builder.Property(movement => movement.Reason).HasMaxLength(2000);
        builder.HasIndex(movement => movement.SchoolId)
            .HasDatabaseName("IX_InventoryMovements_SchoolId");
        builder.HasIndex(movement => new
            {
                movement.SchoolId,
                movement.InventoryItemId,
                movement.OccurredAtUtc
            })
            .HasDatabaseName("IX_InventoryMovements_SchoolId_InventoryItemId_OccurredAtUtc");
        builder.HasOne<School>().WithMany().HasForeignKey(movement => movement.SchoolId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<InventoryItem>().WithMany().HasForeignKey(movement => movement.InventoryItemId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
