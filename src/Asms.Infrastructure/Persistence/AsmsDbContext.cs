using System.Reflection;
using Asms.Application.Abstractions;
using Asms.Domain.Academics;
using Asms.Domain.Attendance;
using Asms.Domain.Auditing;
using Asms.Domain.Communication;
using Asms.Domain.Common;
using Asms.Domain.Examinations;
using Asms.Domain.Finance;
using Asms.Domain.Inventory;
using Asms.Domain.Library;
using Asms.Domain.Schools;
using Asms.Domain.Security;
using Asms.Domain.Staffing;
using Asms.Domain.Students;
using Asms.Domain.Sync;
using Microsoft.EntityFrameworkCore;

namespace Asms.Infrastructure.Persistence;

/// <summary>
/// Provides the EF Core model and tenant-aware persistence.
/// </summary>
public class AsmsDbContext : DbContext
{
    private readonly ICurrentTenant _tenant;

    public AsmsDbContext(DbContextOptions<AsmsDbContext> options, ICurrentTenant tenant)
        : base(options)
    {
        _tenant = tenant;
    }

    /// <summary>
    /// Gets the active school ID, or an empty ID when no school is known.
    /// </summary>
    public Guid CurrentSchoolId => _tenant.SchoolId ?? Guid.Empty;

    public DbSet<AcademicYear> AcademicYears => Set<AcademicYear>();

    public DbSet<Announcement> Announcements => Set<Announcement>();

    public DbSet<Asms.Domain.Attendance.AttendanceRecord> AttendanceRecords =>
        Set<Asms.Domain.Attendance.AttendanceRecord>();

    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();

    public DbSet<AppRole> AppRoles => Set<AppRole>();

    public DbSet<AppUser> AppUsers => Set<AppUser>();

    public DbSet<Book> Books => Set<Book>();

    public DbSet<BookCategory> BookCategories => Set<BookCategory>();

    public DbSet<BookTransaction> BookTransactions => Set<BookTransaction>();

    public DbSet<ClassRoom> ClassRooms => Set<ClassRoom>();

    public DbSet<Enrollment> Enrollments => Set<Enrollment>();

    public DbSet<Examination> Examinations => Set<Examination>();

    public DbSet<ExaminationSubject> ExaminationSubjects => Set<ExaminationSubject>();

    public DbSet<Expense> Expenses => Set<Expense>();

    public DbSet<FeeCategory> FeeCategories => Set<FeeCategory>();

    public DbSet<FeeStructure> FeeStructures => Set<FeeStructure>();

    public DbSet<Grade> Grades => Set<Grade>();

    public DbSet<GradeScale> GradeScales => Set<GradeScale>();

    public DbSet<GradeScaleItem> GradeScaleItems => Set<GradeScaleItem>();

    public DbSet<Guardian> Guardians => Set<Guardian>();

    public DbSet<Asms.Domain.Homework.HomeworkAssignment> HomeworkAssignments =>
        Set<Asms.Domain.Homework.HomeworkAssignment>();

    public DbSet<Asms.Domain.Homework.HomeworkSubmission> HomeworkSubmissions =>
        Set<Asms.Domain.Homework.HomeworkSubmission>();

    public DbSet<InventoryItem> InventoryItems => Set<InventoryItem>();

    public DbSet<InventoryMovement> InventoryMovements => Set<InventoryMovement>();

    public DbSet<Notification> Notifications => Set<Notification>();

    public DbSet<Payment> Payments => Set<Payment>();

    public DbSet<RolePermission> RolePermissions => Set<RolePermission>();

    public DbSet<School> Schools => Set<School>();

    public DbSet<SchoolBranch> SchoolBranches => Set<SchoolBranch>();

    public DbSet<Section> Sections => Set<Section>();

    public DbSet<StaffMember> StaffMembers => Set<StaffMember>();

    public DbSet<Student> Students => Set<Student>();

    public DbSet<StudentGuardian> StudentGuardians => Set<StudentGuardian>();

    public DbSet<StudentInvoice> StudentInvoices => Set<StudentInvoice>();

    public DbSet<StudentMark> StudentMarks => Set<StudentMark>();

    public DbSet<Subject> Subjects => Set<Subject>();

    public DbSet<SyncConflict> SyncConflicts => Set<SyncConflict>();

    public DbSet<SyncDevice> SyncDevices => Set<SyncDevice>();

    public DbSet<SyncOperation> SyncOperations => Set<SyncOperation>();

    public DbSet<Teacher> Teachers => Set<Teacher>();

    public DbSet<Asms.Domain.Staffing.TeacherAssignment> TeacherAssignments =>
        Set<Asms.Domain.Staffing.TeacherAssignment>();

    public DbSet<Asms.Domain.Staffing.TeacherAttendanceRecord> TeacherAttendanceRecords =>
        Set<Asms.Domain.Staffing.TeacherAttendanceRecord>();

    public DbSet<UserRole> UserRoles => Set<UserRole>();

    public DbSet<Asms.Domain.Staffing.LeaveRequest> LeaveRequests =>
        Set<Asms.Domain.Staffing.LeaveRequest>();

    public DbSet<SalaryRecord> SalaryRecords => Set<SalaryRecord>();

    public override int SaveChanges()
    {
        return SaveChanges(acceptAllChangesOnSuccess: true);
    }

    public override int SaveChanges(bool acceptAllChangesOnSuccess)
    {
        StampChanges();
        return base.SaveChanges(acceptAllChangesOnSuccess);
    }

    public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        return SaveChangesAsync(acceptAllChangesOnSuccess: true, cancellationToken);
    }

    public override Task<int> SaveChangesAsync(
        bool acceptAllChangesOnSuccess,
        CancellationToken cancellationToken = default)
    {
        StampChanges();
        return base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AsmsDbContext).Assembly);

        var configureEntity = typeof(AsmsDbContext).GetMethod(
            nameof(ConfigureBaseEntity),
            BindingFlags.Instance | BindingFlags.NonPublic)!;

        var entityTypes = modelBuilder.Model.GetEntityTypes()
            .Where(entityType =>
                typeof(BaseEntity).IsAssignableFrom(entityType.ClrType)
                && !entityType.ClrType.IsAbstract)
            .ToArray();

        foreach (var entityType in entityTypes)
        {
            configureEntity
                .MakeGenericMethod(entityType.ClrType)
                .Invoke(this, new object[] { modelBuilder });
        }

        modelBuilder.Entity<School>().HasQueryFilter(school => !school.IsDeleted);
    }

    private void ConfigureBaseEntity<TEntity>(ModelBuilder modelBuilder)
        where TEntity : BaseEntity
    {
        var entity = modelBuilder.Entity<TEntity>();
        entity.HasKey(item => item.Id);
        entity.Property(item => item.Id).ValueGeneratedNever();
        entity.Property(item => item.Version).IsConcurrencyToken(false);
        entity.HasIndex(item => item.SchoolId);
        entity.HasQueryFilter(item =>
            !item.IsDeleted && item.SchoolId == CurrentSchoolId);
    }

    private void StampChanges()
    {
        var now = DateTime.UtcNow;

        foreach (var entry in ChangeTracker.Entries<BaseEntity>())
        {
            var entity = entry.Entity;

            switch (entry.State)
            {
                case EntityState.Added:
                    if (entity.SchoolId == Guid.Empty && _tenant.SchoolId.HasValue)
                    {
                        entity.SchoolId = _tenant.SchoolId.Value;
                    }

                    if (_tenant.SchoolId.HasValue && entity.SchoolId != _tenant.SchoolId.Value)
                    {
                        throw new InvalidOperationException("Cannot save data for a different school.");
                    }

                    entity.CreatedAtUtc = now;
                    entity.UpdatedAtUtc = now;

                    if (entity.CreatedByUserId is null && _tenant.UserId.HasValue)
                    {
                        entity.CreatedByUserId = _tenant.UserId.Value;
                    }

                    if (entity.UpdatedByUserId is null && _tenant.UserId.HasValue)
                    {
                        entity.UpdatedByUserId = _tenant.UserId.Value;
                    }

                    if (_tenant.DeviceId.HasValue)
                    {
                        entity.LastModifiedByDeviceId = _tenant.DeviceId.Value;
                    }

                    break;

                case EntityState.Modified:
                    if (_tenant.SchoolId.HasValue && entity.SchoolId != _tenant.SchoolId.Value)
                    {
                        throw new InvalidOperationException("Cannot save data for a different school.");
                    }

                    entity.UpdatedAtUtc = now;
                    entity.Version++;

                    if (_tenant.UserId.HasValue)
                    {
                        entity.UpdatedByUserId = _tenant.UserId.Value;
                    }

                    if (_tenant.DeviceId.HasValue)
                    {
                        entity.LastModifiedByDeviceId = _tenant.DeviceId.Value;
                    }

                    break;

                case EntityState.Deleted:
                    throw new InvalidOperationException("Records are never hard-deleted. Use soft delete (IsDeleted).");
            }
        }

        foreach (var entry in ChangeTracker.Entries<School>())
        {
            switch (entry.State)
            {
                case EntityState.Added:
                    entry.Entity.UpdatedAtUtc = now;
                    break;

                case EntityState.Modified:
                    entry.Entity.UpdatedAtUtc = now;
                    entry.Entity.Version++;
                    break;

                case EntityState.Deleted:
                    throw new InvalidOperationException("Records are never hard-deleted. Use soft delete (IsDeleted).");
            }
        }
    }
}
