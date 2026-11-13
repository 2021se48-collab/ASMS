using Asms.Domain.Attendance;
using Asms.Domain.Academics;
using Asms.Domain.Schools;
using Asms.Domain.Staffing;
using Asms.Domain.Students;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Asms.Infrastructure.Persistence.Configurations;

public sealed class AttendanceRecordConfiguration : IEntityTypeConfiguration<AttendanceRecord>
{
    public void Configure(EntityTypeBuilder<AttendanceRecord> builder)
    {
        builder.Property(record => record.Remarks).HasMaxLength(2000);
        builder.HasIndex(record => record.SchoolId)
            .HasDatabaseName("IX_AttendanceRecords_SchoolId");
        builder.HasIndex(record => new
            {
                record.SchoolId,
                record.StudentId,
                record.ClassRoomId,
                record.Date
            })
            .IsUnique()
            .HasDatabaseName("UX_AttendanceRecords_SchoolId_StudentId_ClassRoomId_Date");
        builder.HasIndex(record => new { record.SchoolId, record.ClassRoomId, record.Date })
            .HasDatabaseName("IX_AttendanceRecords_SchoolId_ClassRoomId_Date");
        builder.HasOne<School>().WithMany().HasForeignKey(record => record.SchoolId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Student>().WithMany().HasForeignKey(record => record.StudentId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<ClassRoom>().WithMany().HasForeignKey(record => record.ClassRoomId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Section>().WithMany().HasForeignKey(record => record.SectionId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class TeacherAttendanceRecordConfiguration : IEntityTypeConfiguration<TeacherAttendanceRecord>
{
    public void Configure(EntityTypeBuilder<TeacherAttendanceRecord> builder)
    {
        builder.Property(record => record.Remarks).HasMaxLength(2000);
        builder.HasIndex(record => record.SchoolId)
            .HasDatabaseName("IX_TeacherAttendanceRecords_SchoolId");
        builder.HasIndex(record => new { record.SchoolId, record.TeacherId, record.Date })
            .IsUnique()
            .HasDatabaseName("UX_TeacherAttendanceRecords_SchoolId_TeacherId_Date");
        builder.HasOne<School>().WithMany().HasForeignKey(record => record.SchoolId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Teacher>().WithMany().HasForeignKey(record => record.TeacherId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
