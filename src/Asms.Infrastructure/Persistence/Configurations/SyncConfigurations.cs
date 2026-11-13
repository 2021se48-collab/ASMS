using Asms.Domain.Schools;
using Asms.Domain.Sync;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Asms.Infrastructure.Persistence.Configurations;

public sealed class SyncDeviceConfiguration : IEntityTypeConfiguration<SyncDevice>
{
    public void Configure(EntityTypeBuilder<SyncDevice> builder)
    {
        builder.Property(device => device.DeviceName).IsRequired().HasMaxLength(200);
        builder.Property(device => device.DeviceCode).IsRequired().HasMaxLength(50);
        builder.Property(device => device.Platform).HasMaxLength(100);
        builder.HasIndex(device => device.SchoolId)
            .HasDatabaseName("IX_SyncDevices_SchoolId");
        builder.HasIndex(device => new { device.SchoolId, device.DeviceCode })
            .IsUnique()
            .HasDatabaseName("UX_SyncDevices_SchoolId_DeviceCode");
        builder.HasOne<School>().WithMany().HasForeignKey(device => device.SchoolId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class SyncOperationConfiguration : IEntityTypeConfiguration<SyncOperation>
{
    public void Configure(EntityTypeBuilder<SyncOperation> builder)
    {
        builder.Property(operation => operation.EntityType).IsRequired().HasMaxLength(200);
        builder.Property(operation => operation.PayloadJson).IsRequired();
        builder.Property(operation => operation.LastError).HasMaxLength(2000);
        builder.HasIndex(operation => operation.SchoolId)
            .HasDatabaseName("IX_SyncOperations_SchoolId");
        builder.HasIndex(operation => new { operation.SchoolId, operation.IdempotencyKey })
            .IsUnique()
            .HasDatabaseName("UX_SyncOperations_SchoolId_IdempotencyKey");
        builder.HasIndex(operation => new { operation.SchoolId, operation.Status })
            .HasDatabaseName("IX_SyncOperations_SchoolId_Status");
        builder.HasOne<School>().WithMany().HasForeignKey(operation => operation.SchoolId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<SyncDevice>().WithMany().HasForeignKey(operation => operation.DeviceId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class SyncConflictConfiguration : IEntityTypeConfiguration<SyncConflict>
{
    public void Configure(EntityTypeBuilder<SyncConflict> builder)
    {
        builder.Property(conflict => conflict.EntityType).IsRequired().HasMaxLength(200);
        builder.Property(conflict => conflict.Resolution).HasMaxLength(2000);
        builder.HasIndex(conflict => conflict.SchoolId)
            .HasDatabaseName("IX_SyncConflicts_SchoolId");
        builder.HasIndex(conflict => new { conflict.SchoolId, conflict.IsResolved })
            .HasDatabaseName("IX_SyncConflicts_SchoolId_IsResolved");
        builder.HasOne<School>().WithMany().HasForeignKey(conflict => conflict.SchoolId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<SyncOperation>().WithMany().HasForeignKey(conflict => conflict.SyncOperationId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
