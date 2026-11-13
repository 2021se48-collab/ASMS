using Asms.Domain.Academics;
using Asms.Domain.Examinations;
using Asms.Domain.Schools;
using Asms.Domain.Students;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Asms.Infrastructure.Persistence.Configurations;

public sealed class ExaminationConfiguration : IEntityTypeConfiguration<Examination>
{
    public void Configure(EntityTypeBuilder<Examination> builder)
    {
        builder.Property(examination => examination.Name).IsRequired().HasMaxLength(200);
        builder.HasIndex(examination => examination.SchoolId)
            .HasDatabaseName("IX_Examinations_SchoolId");
        builder.HasIndex(examination => new { examination.SchoolId, examination.AcademicYearId })
            .HasDatabaseName("IX_Examinations_SchoolId_AcademicYearId");
        builder.HasOne<School>().WithMany().HasForeignKey(examination => examination.SchoolId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<AcademicYear>().WithMany().HasForeignKey(examination => examination.AcademicYearId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class ExaminationSubjectConfiguration : IEntityTypeConfiguration<ExaminationSubject>
{
    public void Configure(EntityTypeBuilder<ExaminationSubject> builder)
    {
        builder.Property(subject => subject.MaxMarks).HasPrecision(18, 2);
        builder.Property(subject => subject.PassMarks).HasPrecision(18, 2);
        builder.HasIndex(subject => subject.SchoolId)
            .HasDatabaseName("IX_ExaminationSubjects_SchoolId");
        builder.HasIndex(subject => new
            {
                subject.SchoolId,
                subject.ExaminationId,
                subject.SubjectId,
                subject.ClassRoomId
            })
            .IsUnique()
            .HasDatabaseName("UX_ExaminationSubjects_SchoolId_ExaminationId_SubjectId_ClassRoomId");
        builder.HasOne<School>().WithMany().HasForeignKey(subject => subject.SchoolId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Examination>().WithMany().HasForeignKey(subject => subject.ExaminationId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Subject>().WithMany().HasForeignKey(subject => subject.SubjectId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<ClassRoom>().WithMany().HasForeignKey(subject => subject.ClassRoomId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class StudentMarkConfiguration : IEntityTypeConfiguration<StudentMark>
{
    public void Configure(EntityTypeBuilder<StudentMark> builder)
    {
        builder.Property(mark => mark.ObtainedMarks).HasPrecision(18, 2);
        builder.Property(mark => mark.Remarks).HasMaxLength(2000);
        builder.HasIndex(mark => mark.SchoolId).HasDatabaseName("IX_StudentMarks_SchoolId");
        builder.HasIndex(mark => new { mark.SchoolId, mark.ExaminationSubjectId, mark.StudentId })
            .IsUnique()
            .HasDatabaseName("UX_StudentMarks_SchoolId_ExaminationSubjectId_StudentId");
        builder.HasOne<School>().WithMany().HasForeignKey(mark => mark.SchoolId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<ExaminationSubject>().WithMany().HasForeignKey(mark => mark.ExaminationSubjectId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Student>().WithMany().HasForeignKey(mark => mark.StudentId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class GradeScaleConfiguration : IEntityTypeConfiguration<GradeScale>
{
    public void Configure(EntityTypeBuilder<GradeScale> builder)
    {
        builder.Property(scale => scale.Name).IsRequired().HasMaxLength(200);
        builder.HasIndex(scale => scale.SchoolId).HasDatabaseName("IX_GradeScales_SchoolId");
        builder.HasIndex(scale => new { scale.SchoolId, scale.Name })
            .IsUnique()
            .HasDatabaseName("UX_GradeScales_SchoolId_Name");
        builder.HasOne<School>().WithMany().HasForeignKey(scale => scale.SchoolId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class GradeScaleItemConfiguration : IEntityTypeConfiguration<GradeScaleItem>
{
    public void Configure(EntityTypeBuilder<GradeScaleItem> builder)
    {
        builder.Property(item => item.GradeName).IsRequired().HasMaxLength(200);
        builder.Property(item => item.MinPercentage).HasPrecision(5, 2);
        builder.Property(item => item.MaxPercentage).HasPrecision(5, 2);
        builder.HasIndex(item => item.SchoolId).HasDatabaseName("IX_GradeScaleItems_SchoolId");
        builder.HasIndex(item => new { item.SchoolId, item.GradeScaleId, item.GradeName })
            .IsUnique()
            .HasDatabaseName("UX_GradeScaleItems_SchoolId_GradeScaleId_GradeName");
        builder.HasOne<School>().WithMany().HasForeignKey(item => item.SchoolId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<GradeScale>().WithMany().HasForeignKey(item => item.GradeScaleId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
