using Asms.Domain.Schools;
using Asms.Domain.Students;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Asms.Infrastructure.Persistence.Configurations;

public sealed class StudentConfiguration : IEntityTypeConfiguration<Student>
{
    public void Configure(EntityTypeBuilder<Student> builder)
    {
        builder.Property(student => student.StudentNumber).IsRequired().HasMaxLength(50);
        builder.Property(student => student.FirstName).IsRequired().HasMaxLength(200);
        builder.Property(student => student.LastName).IsRequired().HasMaxLength(200);
        builder.Property(student => student.FatherName).IsRequired().HasMaxLength(200);
        builder.Property(student => student.GrandfatherName).HasMaxLength(200);
        builder.Property(student => student.Province).HasMaxLength(200);
        builder.Property(student => student.District).HasMaxLength(200);
        builder.Property(student => student.Address).HasMaxLength(500);
        builder.Property(student => student.PhoneNumber).HasMaxLength(30);
        builder.Property(student => student.PreviousSchool).HasMaxLength(200);
        builder.Property(student => student.PhotoPath).HasMaxLength(500);
        builder.Property(student => student.DuplicateKey).IsRequired().HasMaxLength(500);

        builder.HasIndex(student => student.SchoolId)
            .HasDatabaseName("IX_Students_SchoolId");
        builder.HasIndex(student => new { student.SchoolId, student.StudentNumber })
            .IsUnique()
            .HasDatabaseName("UX_Students_SchoolId_StudentNumber");
        builder.HasIndex(student => new { student.SchoolId, student.DuplicateKey })
            .HasDatabaseName("IX_Students_SchoolId_DuplicateKey");
        builder.HasIndex(student => new { student.SchoolId, student.LastName, student.FirstName })
            .HasDatabaseName("IX_Students_SchoolId_LastName_FirstName");
        builder.HasIndex(student => new { student.SchoolId, student.Status })
            .HasDatabaseName("IX_Students_SchoolId_Status");
        builder.HasOne<School>()
            .WithMany()
            .HasForeignKey(student => student.SchoolId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class GuardianConfiguration : IEntityTypeConfiguration<Guardian>
{
    public void Configure(EntityTypeBuilder<Guardian> builder)
    {
        builder.Property(guardian => guardian.FullName).IsRequired().HasMaxLength(200);
        builder.Property(guardian => guardian.PhoneNumber).HasMaxLength(30);
        builder.Property(guardian => guardian.Email).HasMaxLength(254);
        builder.Property(guardian => guardian.NationalIdNumber).HasMaxLength(50);
        builder.Property(guardian => guardian.Address).HasMaxLength(500);
        builder.Property(guardian => guardian.Occupation).HasMaxLength(200);

        builder.HasIndex(guardian => guardian.SchoolId)
            .HasDatabaseName("IX_Guardians_SchoolId");
        builder.HasIndex(guardian => new { guardian.SchoolId, guardian.PhoneNumber })
            .HasDatabaseName("IX_Guardians_SchoolId_PhoneNumber");
        builder.HasOne<School>()
            .WithMany()
            .HasForeignKey(guardian => guardian.SchoolId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class StudentGuardianConfiguration : IEntityTypeConfiguration<StudentGuardian>
{
    public void Configure(EntityTypeBuilder<StudentGuardian> builder)
    {
        builder.HasIndex(link => link.SchoolId)
            .HasDatabaseName("IX_StudentGuardians_SchoolId");
        builder.HasIndex(link => new { link.SchoolId, link.StudentId, link.GuardianId })
            .IsUnique()
            .HasDatabaseName("UX_StudentGuardians_SchoolId_StudentId_GuardianId");
        builder.HasOne<School>()
            .WithMany()
            .HasForeignKey(link => link.SchoolId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Student>()
            .WithMany()
            .HasForeignKey(link => link.StudentId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Guardian>()
            .WithMany()
            .HasForeignKey(link => link.GuardianId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
