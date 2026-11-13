using Asms.Domain.Auditing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Asms.Infrastructure.Persistence.Configurations;

public sealed class AuditLogConfiguration : IEntityTypeConfiguration<AuditLog>
{
    public void Configure(EntityTypeBuilder<AuditLog> builder)
    {
        builder.Property(log => log.Action).IsRequired().HasMaxLength(100);
        builder.Property(log => log.EntityType).IsRequired().HasMaxLength(200);
        builder.Property(log => log.IpAddress).HasMaxLength(45);

        builder.HasIndex(log => log.SchoolId)
            .HasDatabaseName("IX_AuditLogs_SchoolId");
        builder.HasIndex(log => new { log.SchoolId, log.OccurredAtUtc })
            .HasDatabaseName("IX_AuditLogs_SchoolId_OccurredAtUtc");
        builder.HasIndex(log => new { log.SchoolId, log.EntityType, log.EntityId })
            .HasDatabaseName("IX_AuditLogs_SchoolId_EntityType_EntityId");
    }
}
