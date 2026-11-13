using Asms.Domain.Academics;
using Asms.Domain.Homework;
using Asms.Domain.Schools;
using Asms.Domain.Staffing;
using Asms.Domain.Students;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Asms.Infrastructure.Persistence.Configurations;

public sealed class HomeworkAssignmentConfiguration : IEntityTypeConfiguration<HomeworkAssignment>
{
    public void Configure(EntityTypeBuilder<HomeworkAssignment> builder)
    {
        builder.Property(assignment => assignment.Title).IsRequired().HasMaxLength(200);
        builder.Property(assignment => assignment.Description).HasMaxLength(2000);
        builder.HasIndex(assignment => assignment.SchoolId)
            .HasDatabaseName("IX_HomeworkAssignments_SchoolId");
        builder.HasIndex(assignment => new
            {
                assignment.SchoolId,
                assignment.ClassRoomId,
                assignment.DueDate
            })
            .HasDatabaseName("IX_HomeworkAssignments_SchoolId_ClassRoomId_DueDate");
        builder.HasOne<School>().WithMany().HasForeignKey(assignment => assignment.SchoolId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Teacher>().WithMany().HasForeignKey(assignment => assignment.TeacherId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<ClassRoom>().WithMany().HasForeignKey(assignment => assignment.ClassRoomId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Section>().WithMany().HasForeignKey(assignment => assignment.SectionId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Subject>().WithMany().HasForeignKey(assignment => assignment.SubjectId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class HomeworkSubmissionConfiguration : IEntityTypeConfiguration<HomeworkSubmission>
{
    public void Configure(EntityTypeBuilder<HomeworkSubmission> builder)
    {
        builder.Property(submission => submission.SubmissionText).HasMaxLength(2000);
        builder.Property(submission => submission.AttachmentPath).HasMaxLength(500);
        builder.Property(submission => submission.TeacherFeedback).HasMaxLength(2000);
        builder.Property(submission => submission.Score).HasPrecision(18, 2);
        builder.HasIndex(submission => submission.SchoolId)
            .HasDatabaseName("IX_HomeworkSubmissions_SchoolId");
        builder.HasIndex(submission => new
            {
                submission.SchoolId,
                submission.HomeworkAssignmentId,
                submission.StudentId
            })
            .IsUnique()
            .HasDatabaseName("UX_HomeworkSubmissions_SchoolId_HomeworkAssignmentId_StudentId");
        builder.HasOne<School>().WithMany().HasForeignKey(submission => submission.SchoolId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<HomeworkAssignment>().WithMany()
            .HasForeignKey(submission => submission.HomeworkAssignmentId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Student>().WithMany().HasForeignKey(submission => submission.StudentId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
