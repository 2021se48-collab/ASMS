using Asms.Domain.Academics;
using Asms.Domain.Schools;
using Asms.Domain.Students;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Asms.Infrastructure.Persistence.Configurations;

public sealed class AcademicYearConfiguration : IEntityTypeConfiguration<AcademicYear>
{
    public void Configure(EntityTypeBuilder<AcademicYear> builder)
    {
        builder.Property(year => year.Name).IsRequired().HasMaxLength(200);
        builder.HasIndex(year => year.SchoolId).HasDatabaseName("IX_AcademicYears_SchoolId");
        builder.HasIndex(year => new { year.SchoolId, year.Name })
            .IsUnique()
            .HasDatabaseName("UX_AcademicYears_SchoolId_Name");
        builder.HasOne<School>().WithMany().HasForeignKey(year => year.SchoolId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class GradeConfiguration : IEntityTypeConfiguration<Grade>
{
    public void Configure(EntityTypeBuilder<Grade> builder)
    {
        builder.Property(grade => grade.Name).IsRequired().HasMaxLength(200);
        builder.HasIndex(grade => grade.SchoolId).HasDatabaseName("IX_Grades_SchoolId");
        builder.HasIndex(grade => new { grade.SchoolId, grade.Name })
            .IsUnique()
            .HasDatabaseName("UX_Grades_SchoolId_Name");
        builder.HasOne<School>().WithMany().HasForeignKey(grade => grade.SchoolId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class ClassRoomConfiguration : IEntityTypeConfiguration<ClassRoom>
{
    public void Configure(EntityTypeBuilder<ClassRoom> builder)
    {
        builder.Property(classRoom => classRoom.Name).IsRequired().HasMaxLength(200);
        builder.HasIndex(classRoom => classRoom.SchoolId)
            .HasDatabaseName("IX_ClassRooms_SchoolId");
        builder.HasIndex(classRoom => new
            {
                classRoom.SchoolId,
                classRoom.AcademicYearId,
                classRoom.GradeId,
                classRoom.BranchId,
                classRoom.Name
            })
            .IsUnique()
            .HasDatabaseName("UX_ClassRooms_SchoolId_AcademicYearId_GradeId_BranchId_Name");
        builder.HasOne<School>().WithMany().HasForeignKey(classRoom => classRoom.SchoolId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Grade>().WithMany().HasForeignKey(classRoom => classRoom.GradeId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<AcademicYear>().WithMany().HasForeignKey(classRoom => classRoom.AcademicYearId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<SchoolBranch>().WithMany().HasForeignKey(classRoom => classRoom.BranchId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class SectionConfiguration : IEntityTypeConfiguration<Section>
{
    public void Configure(EntityTypeBuilder<Section> builder)
    {
        builder.Property(section => section.Name).IsRequired().HasMaxLength(200);
        builder.HasIndex(section => section.SchoolId).HasDatabaseName("IX_Sections_SchoolId");
        builder.HasIndex(section => new { section.SchoolId, section.ClassRoomId, section.Name })
            .IsUnique()
            .HasDatabaseName("UX_Sections_SchoolId_ClassRoomId_Name");
        builder.HasOne<School>().WithMany().HasForeignKey(section => section.SchoolId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<ClassRoom>().WithMany().HasForeignKey(section => section.ClassRoomId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class SubjectConfiguration : IEntityTypeConfiguration<Subject>
{
    public void Configure(EntityTypeBuilder<Subject> builder)
    {
        builder.Property(subject => subject.Name).IsRequired().HasMaxLength(200);
        builder.Property(subject => subject.Code).HasMaxLength(50);
        builder.HasIndex(subject => subject.SchoolId).HasDatabaseName("IX_Subjects_SchoolId");
        builder.HasIndex(subject => new { subject.SchoolId, subject.Name })
            .IsUnique()
            .HasDatabaseName("UX_Subjects_SchoolId_Name");
        builder.HasIndex(subject => new { subject.SchoolId, subject.Code })
            .IsUnique()
            .HasDatabaseName("UX_Subjects_SchoolId_Code");
        builder.HasOne<School>().WithMany().HasForeignKey(subject => subject.SchoolId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class EnrollmentConfiguration : IEntityTypeConfiguration<Enrollment>
{
    public void Configure(EntityTypeBuilder<Enrollment> builder)
    {
        builder.Property(enrollment => enrollment.Notes).HasMaxLength(2000);
        builder.HasIndex(enrollment => enrollment.SchoolId)
            .HasDatabaseName("IX_Enrollments_SchoolId");
        builder.HasIndex(enrollment => new
            {
                enrollment.SchoolId,
                enrollment.StudentId,
                enrollment.AcademicYearId
            })
            .IsUnique()
            .HasDatabaseName("UX_Enrollments_SchoolId_StudentId_AcademicYearId");
        builder.HasIndex(enrollment => new { enrollment.SchoolId, enrollment.ClassRoomId })
            .HasDatabaseName("IX_Enrollments_SchoolId_ClassRoomId");
        builder.HasOne<School>().WithMany().HasForeignKey(enrollment => enrollment.SchoolId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Student>().WithMany().HasForeignKey(enrollment => enrollment.StudentId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<AcademicYear>().WithMany().HasForeignKey(enrollment => enrollment.AcademicYearId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<ClassRoom>().WithMany().HasForeignKey(enrollment => enrollment.ClassRoomId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Section>().WithMany().HasForeignKey(enrollment => enrollment.SectionId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
