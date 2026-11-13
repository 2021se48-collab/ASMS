using Asms.Domain.Schools;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Asms.Infrastructure.Persistence.Configurations;

public sealed class SchoolConfiguration : IEntityTypeConfiguration<School>
{
    public void Configure(EntityTypeBuilder<School> builder)
    {
        builder.HasKey(school => school.Id);
        builder.Property(school => school.Name).IsRequired().HasMaxLength(200);
        builder.Property(school => school.Code).HasMaxLength(50);
        builder.Property(school => school.Province).HasMaxLength(200);
        builder.Property(school => school.District).HasMaxLength(200);
        builder.Property(school => school.Address).HasMaxLength(500);
        builder.Property(school => school.PhoneNumber).HasMaxLength(30);
        builder.Property(school => school.Email).HasMaxLength(254);
        builder.Property(school => school.DefaultLanguage).IsRequired().HasMaxLength(10);
        builder.Property(school => school.CalendarType).IsRequired().HasMaxLength(50);

        builder.HasIndex(school => school.Code)
            .IsUnique()
            .HasDatabaseName("UX_Schools_Code");
    }
}

public sealed class SchoolBranchConfiguration : IEntityTypeConfiguration<SchoolBranch>
{
    public void Configure(EntityTypeBuilder<SchoolBranch> builder)
    {
        builder.Property(branch => branch.Name).IsRequired().HasMaxLength(200);
        builder.Property(branch => branch.Code).HasMaxLength(50);
        builder.Property(branch => branch.Province).HasMaxLength(200);
        builder.Property(branch => branch.District).HasMaxLength(200);
        builder.Property(branch => branch.Address).HasMaxLength(500);
        builder.Property(branch => branch.PhoneNumber).HasMaxLength(30);

        builder.HasIndex(branch => branch.SchoolId)
            .HasDatabaseName("IX_SchoolBranches_SchoolId");
        builder.HasIndex(branch => new { branch.SchoolId, branch.Code })
            .IsUnique()
            .HasDatabaseName("UX_SchoolBranches_SchoolId_Code");
        builder.HasOne<School>()
            .WithMany()
            .HasForeignKey(branch => branch.SchoolId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
