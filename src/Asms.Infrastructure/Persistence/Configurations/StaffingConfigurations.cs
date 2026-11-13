using Asms.Domain.Academics;
using Asms.Domain.Schools;
using Asms.Domain.Staffing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Asms.Infrastructure.Persistence.Configurations;

public sealed class TeacherConfiguration : IEntityTypeConfiguration<Teacher>
{
    public void Configure(EntityTypeBuilder<Teacher> builder)
    {
        builder.Property(teacher => teacher.EmployeeNumber).IsRequired().HasMaxLength(50);
        builder.Property(teacher => teacher.FirstName).IsRequired().HasMaxLength(200);
        builder.Property(teacher => teacher.LastName).IsRequired().HasMaxLength(200);
        builder.Property(teacher => teacher.FatherName).HasMaxLength(200);
        builder.Property(teacher => teacher.PhoneNumber).HasMaxLength(30);
        builder.Property(teacher => teacher.Email).HasMaxLength(254);
        builder.Property(teacher => teacher.Province).HasMaxLength(200);
        builder.Property(teacher => teacher.District).HasMaxLength(200);
        builder.Property(teacher => teacher.Address).HasMaxLength(500);
        builder.Property(teacher => teacher.Qualification).HasMaxLength(200);
        builder.Property(teacher => teacher.PhotoPath).HasMaxLength(500);

        builder.HasIndex(teacher => teacher.SchoolId)
            .HasDatabaseName("IX_Teachers_SchoolId");
        builder.HasIndex(teacher => new { teacher.SchoolId, teacher.EmployeeNumber })
            .IsUnique()
            .HasDatabaseName("UX_Teachers_SchoolId_EmployeeNumber");
        builder.HasIndex(teacher => new { teacher.SchoolId, teacher.LastName, teacher.FirstName })
            .HasDatabaseName("IX_Teachers_SchoolId_LastName_FirstName");
        builder.HasOne<School>().WithMany().HasForeignKey(teacher => teacher.SchoolId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class StaffMemberConfiguration : IEntityTypeConfiguration<StaffMember>
{
    public void Configure(EntityTypeBuilder<StaffMember> builder)
    {
        builder.Property(staff => staff.EmployeeNumber).IsRequired().HasMaxLength(50);
        builder.Property(staff => staff.FirstName).IsRequired().HasMaxLength(200);
        builder.Property(staff => staff.LastName).IsRequired().HasMaxLength(200);
        builder.Property(staff => staff.FatherName).HasMaxLength(200);
        builder.Property(staff => staff.PhoneNumber).HasMaxLength(30);
        builder.Property(staff => staff.Email).HasMaxLength(254);
        builder.Property(staff => staff.Province).HasMaxLength(200);
        builder.Property(staff => staff.District).HasMaxLength(200);
        builder.Property(staff => staff.Address).HasMaxLength(500);
        builder.Property(staff => staff.JobTitle).HasMaxLength(200);
        builder.Property(staff => staff.Department).HasMaxLength(200);
        builder.Property(staff => staff.PhotoPath).HasMaxLength(500);

        builder.HasIndex(staff => staff.SchoolId)
            .HasDatabaseName("IX_StaffMembers_SchoolId");
        builder.HasIndex(staff => new { staff.SchoolId, staff.EmployeeNumber })
            .IsUnique()
            .HasDatabaseName("UX_StaffMembers_SchoolId_EmployeeNumber");
        builder.HasOne<School>().WithMany().HasForeignKey(staff => staff.SchoolId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class TeacherAssignmentConfiguration : IEntityTypeConfiguration<TeacherAssignment>
{
    public void Configure(EntityTypeBuilder<TeacherAssignment> builder)
    {
        builder.HasIndex(assignment => assignment.SchoolId)
            .HasDatabaseName("IX_TeacherAssignments_SchoolId");
        builder.HasIndex(assignment => new
            {
                assignment.SchoolId,
                assignment.TeacherId,
                assignment.SubjectId,
                assignment.ClassRoomId,
                assignment.SectionId,
                assignment.AcademicYearId
            })
            .IsUnique()
            .HasDatabaseName("UX_TeacherAssignments_SchoolId_TeacherId_SubjectId_ClassRoomId_SectionId_AcademicYearId");
        builder.HasOne<School>().WithMany().HasForeignKey(assignment => assignment.SchoolId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Teacher>().WithMany().HasForeignKey(assignment => assignment.TeacherId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Subject>().WithMany().HasForeignKey(assignment => assignment.SubjectId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<ClassRoom>().WithMany().HasForeignKey(assignment => assignment.ClassRoomId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Section>().WithMany().HasForeignKey(assignment => assignment.SectionId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<AcademicYear>().WithMany().HasForeignKey(assignment => assignment.AcademicYearId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class LeaveRequestConfiguration : IEntityTypeConfiguration<LeaveRequest>
{
    public void Configure(EntityTypeBuilder<LeaveRequest> builder)
    {
        builder.Property(request => request.Reason).HasMaxLength(2000);
        builder.HasIndex(request => request.SchoolId)
            .HasDatabaseName("IX_LeaveRequests_SchoolId");
        builder.HasIndex(request => new { request.SchoolId, request.EmployeeId, request.StartDate })
            .HasDatabaseName("IX_LeaveRequests_SchoolId_EmployeeId_StartDate");
        builder.HasOne<School>().WithMany().HasForeignKey(request => request.SchoolId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class SalaryRecordConfiguration : IEntityTypeConfiguration<SalaryRecord>
{
    public void Configure(EntityTypeBuilder<SalaryRecord> builder)
    {
        builder.Property(record => record.BasicSalary).HasPrecision(18, 2);
        builder.Property(record => record.Allowances).HasPrecision(18, 2);
        builder.Property(record => record.Deductions).HasPrecision(18, 2);
        builder.Property(record => record.NetSalary).HasPrecision(18, 2);
        builder.Property(record => record.Currency).IsRequired().HasMaxLength(3);
        builder.Property(record => record.Notes).HasMaxLength(2000);
        builder.HasIndex(record => record.SchoolId)
            .HasDatabaseName("IX_SalaryRecords_SchoolId");
        builder.HasIndex(record => new
            {
                record.SchoolId,
                record.EmployeeId,
                record.PeriodStart,
                record.PeriodEnd
            })
            .IsUnique()
            .HasDatabaseName("UX_SalaryRecords_SchoolId_EmployeeId_PeriodStart_PeriodEnd");
        builder.HasOne<School>().WithMany().HasForeignKey(record => record.SchoolId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
