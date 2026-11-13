using Asms.Domain.Academics;
using Asms.Domain.Auditing;
using Asms.Domain.Attendance;
using Asms.Domain.Common;
using Asms.Domain.Examinations;
using Asms.Domain.Finance;
using Asms.Domain.Schools;
using Asms.Domain.Security;
using Asms.Domain.Students;
using Asms.Domain.Sync;
using Asms.Infrastructure.Persistence;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;

namespace Asms.Infrastructure.Tests.Persistence;

public class ConfigurationTests
{
    [Fact]
    public void Student_number_is_unique_per_school()
    {
        using var connection = CreateOpenConnection();
        var schoolA = Guid.NewGuid();
        var schoolB = Guid.NewGuid();
        using (var seedContext = CreateContext(connection, null))
        {
            seedContext.Schools.AddRange(
                new School { Id = schoolA, Name = "School A" },
                new School { Id = schoolB, Name = "School B" });
            seedContext.SaveChanges();
        }

        using (var contextA = CreateContext(connection, schoolA))
        {
            contextA.Students.Add(NewStudent(schoolA, "ST-001"));
            contextA.SaveChanges();

            contextA.Students.Add(NewStudent(schoolA, "ST-001"));
            Assert.Throws<DbUpdateException>(() => contextA.SaveChanges());
        }

        using var contextB = CreateContext(connection, schoolB);
        contextB.Students.Add(NewStudent(schoolB, "ST-001"));
        contextB.SaveChanges();
    }

    [Fact]
    public void Attendance_student_class_and_date_are_unique()
    {
        using var connection = CreateOpenConnection();
        var schoolId = Guid.NewGuid();
        using var context = CreateContext(connection, schoolId);
        var (student, classRoom) = AddStudentAndClassRoom(context, schoolId);
        var date = new DateOnly(2025, 1, 15);

        context.AttendanceRecords.Add(NewAttendance(schoolId, student.Id, classRoom.Id, date));
        context.SaveChanges();

        var duplicate = NewAttendance(schoolId, student.Id, classRoom.Id, date);
        context.AttendanceRecords.Add(duplicate);
        Assert.Throws<DbUpdateException>(() => context.SaveChanges());
        context.Entry(duplicate).State = EntityState.Detached;

        context.AttendanceRecords.Add(NewAttendance(schoolId, student.Id, classRoom.Id, date.AddDays(1)));
        context.SaveChanges();
    }

    [Fact]
    public void Payment_receipt_number_is_unique_per_school()
    {
        using var connection = CreateOpenConnection();
        var schoolA = Guid.NewGuid();
        var schoolB = Guid.NewGuid();
        SeedSchoolStudentAndInvoice(connection, schoolA, "ST-A", "INV-A");
        SeedSchoolStudentAndInvoice(connection, schoolB, "ST-B", "INV-B");

        using (var contextA = CreateContext(connection, schoolA))
        {
            var student = contextA.Students.IgnoreQueryFilters()
                .Single(item => item.SchoolId == schoolA);
            var invoice = contextA.StudentInvoices.IgnoreQueryFilters()
                .Single(item => item.SchoolId == schoolA);
            contextA.Payments.Add(NewPayment(schoolA, student.Id, invoice.Id, "RCPT-001"));
            contextA.SaveChanges();

            contextA.Payments.Add(NewPayment(schoolA, student.Id, invoice.Id, "RCPT-001"));
            Assert.Throws<DbUpdateException>(() => contextA.SaveChanges());
        }

        using var contextB = CreateContext(connection, schoolB);
        var studentB = contextB.Students.IgnoreQueryFilters()
            .Single(item => item.SchoolId == schoolB);
        var invoiceB = contextB.StudentInvoices.IgnoreQueryFilters()
            .Single(item => item.SchoolId == schoolB);
        contextB.Payments.Add(NewPayment(schoolB, studentB.Id, invoiceB.Id, "RCPT-001"));
        contextB.SaveChanges();
    }

    [Fact]
    public void Student_invoice_number_is_unique_per_school()
    {
        using var connection = CreateOpenConnection();
        var schoolId = Guid.NewGuid();
        using var context = CreateContext(connection, schoolId);
        var (student, academicYear) = AddStudentAndAcademicYear(context, schoolId);

        context.StudentInvoices.Add(NewInvoice(schoolId, student.Id, academicYear.Id, "INV-001"));
        context.SaveChanges();
        context.StudentInvoices.Add(NewInvoice(schoolId, student.Id, academicYear.Id, "INV-001"));

        Assert.Throws<DbUpdateException>(() => context.SaveChanges());
    }

    [Fact]
    public void Student_can_only_be_enrolled_once_per_academic_year()
    {
        using var connection = CreateOpenConnection();
        var schoolId = Guid.NewGuid();
        using var context = CreateContext(connection, schoolId);
        var (student, classRoom) = AddStudentAndClassRoom(context, schoolId);

        context.Enrollments.Add(new Enrollment
        {
            SchoolId = schoolId,
            StudentId = student.Id,
            AcademicYearId = classRoom.AcademicYearId,
            ClassRoomId = classRoom.Id
        });
        context.SaveChanges();
        context.Enrollments.Add(new Enrollment
        {
            SchoolId = schoolId,
            StudentId = student.Id,
            AcademicYearId = classRoom.AcademicYearId,
            ClassRoomId = classRoom.Id
        });

        Assert.Throws<DbUpdateException>(() => context.SaveChanges());
    }

    [Fact]
    public void Sync_idempotency_key_is_unique_per_school()
    {
        using var connection = CreateOpenConnection();
        var schoolId = Guid.NewGuid();
        using var context = CreateContext(connection, schoolId);
        AddSchool(context, schoolId);
        var device = new SyncDevice
        {
            SchoolId = schoolId,
            DeviceName = "Device",
            DeviceCode = "DEV-001"
        };
        context.SyncDevices.Add(device);
        context.SaveChanges();
        var idempotencyKey = Guid.NewGuid();

        context.SyncOperations.Add(NewSyncOperation(schoolId, device.Id, idempotencyKey));
        context.SaveChanges();
        context.SyncOperations.Add(NewSyncOperation(schoolId, device.Id, idempotencyKey));

        Assert.Throws<DbUpdateException>(() => context.SaveChanges());
    }

    [Fact]
    public void Soft_deleted_student_number_cannot_be_reused()
    {
        using var connection = CreateOpenConnection();
        var schoolId = Guid.NewGuid();
        using var context = CreateContext(connection, schoolId);
        AddSchool(context, schoolId);
        var student = NewStudent(schoolId, "ST-001");
        context.Students.Add(student);
        context.SaveChanges();
        student.IsDeleted = true;
        context.SaveChanges();

        context.Students.Add(NewStudent(schoolId, "ST-001"));

        Assert.Throws<DbUpdateException>(() => context.SaveChanges());
    }

    [Fact]
    public void Model_has_requested_property_lengths_and_precision()
    {
        using var connection = CreateOpenConnection();
        using var context = CreateContext(connection, null);
        var model = context.Model;

        Assert.Equal(200, model.FindEntityType(typeof(Student))!
            .FindProperty(nameof(Student.FirstName))!.GetMaxLength());
        Assert.Equal(50, model.FindEntityType(typeof(Student))!
            .FindProperty(nameof(Student.StudentNumber))!.GetMaxLength());
        AssertPrecision(model, typeof(Payment), nameof(Payment.Amount), 18, 2);
        AssertPrecision(model, typeof(StudentMark), nameof(StudentMark.ObtainedMarks), 18, 2);
        AssertPrecision(model, typeof(GradeScaleItem), nameof(GradeScaleItem.MinPercentage), 5, 2);
    }

    [Fact]
    public void Every_model_index_has_an_explicitly_prefixed_name()
    {
        using var connection = CreateOpenConnection();
        using var context = CreateContext(connection, null);

        var unnamedOrInvalid = context.Model.GetEntityTypes()
            .SelectMany(entity => entity.GetIndexes())
            .Where(index =>
            {
                var name = index.GetDatabaseName();
                return name is null ||
                    (!name.StartsWith("IX_", StringComparison.Ordinal) &&
                     !name.StartsWith("UX_", StringComparison.Ordinal));
            })
            .ToList();

        Assert.Empty(unnamedOrInvalid);
    }

    [Fact]
    public void Every_foreign_key_uses_restrict_delete_behavior()
    {
        using var connection = CreateOpenConnection();
        using var context = CreateContext(connection, null);

        var foreignKeys = context.Model.GetEntityTypes()
            .SelectMany(entity => entity.GetForeignKeys())
            .ToList();

        Assert.NotEmpty(foreignKeys);
        Assert.All(foreignKeys, foreignKey => Assert.Equal(DeleteBehavior.Restrict, foreignKey.DeleteBehavior));
    }

    [Fact]
    public void Tenant_entities_have_school_foreign_keys_except_platform_entities()
    {
        using var connection = CreateOpenConnection();
        using var context = CreateContext(connection, null);
        var excludedTypes = new[]
        {
            typeof(AppUser),
            typeof(UserRole),
            typeof(AuditLog)
        };
        var entities = context.Model.GetEntityTypes()
            .Where(entity => typeof(BaseEntity).IsAssignableFrom(entity.ClrType) &&
                entity.ClrType != typeof(BaseEntity) &&
                !excludedTypes.Contains(entity.ClrType))
            .ToList();

        Assert.NotEmpty(entities);
        Assert.All(entities, entity =>
            Assert.Contains(entity.GetForeignKeys(), foreignKey =>
                foreignKey.PrincipalEntityType.ClrType == typeof(School) &&
                foreignKey.Properties.Any(property => property.Name == nameof(BaseEntity.SchoolId))));
    }

    private static void AssertPrecision(
        IModel model,
        Type entityType,
        string propertyName,
        int precision,
        int scale)
    {
        var property = model.FindEntityType(entityType)!.FindProperty(propertyName)!;
        Assert.Equal(precision, property.GetPrecision());
        Assert.Equal(scale, property.GetScale());
    }

    private static (Student Student, ClassRoom ClassRoom) AddStudentAndClassRoom(
        AsmsDbContext context,
        Guid schoolId)
    {
        AddSchool(context, schoolId);
        var academicYear = NewAcademicYear(schoolId);
        var grade = new Grade { SchoolId = schoolId, Name = "Grade 1" };
        var student = NewStudent(schoolId, "ST-001");
        context.AddRange(academicYear, grade, student);
        context.SaveChanges();

        var classRoom = new ClassRoom
        {
            SchoolId = schoolId,
            AcademicYearId = academicYear.Id,
            GradeId = grade.Id,
            Name = "Room A"
        };
        context.ClassRooms.Add(classRoom);
        context.SaveChanges();
        return (student, classRoom);
    }

    private static (Student Student, AcademicYear AcademicYear) AddStudentAndAcademicYear(
        AsmsDbContext context,
        Guid schoolId)
    {
        AddSchool(context, schoolId);
        var academicYear = NewAcademicYear(schoolId);
        var student = NewStudent(schoolId, "ST-001");
        context.AddRange(academicYear, student);
        context.SaveChanges();
        return (student, academicYear);
    }

    private static void SeedSchoolStudentAndInvoice(
        SqliteConnection connection,
        Guid schoolId,
        string studentNumber,
        string invoiceNumber)
    {
        using var context = CreateContext(connection, schoolId);
        AddSchool(context, schoolId);
        var academicYear = NewAcademicYear(schoolId);
        var student = NewStudent(schoolId, studentNumber);
        context.AddRange(academicYear, student);
        context.SaveChanges();
        context.StudentInvoices.Add(NewInvoice(schoolId, student.Id, academicYear.Id, invoiceNumber));
        context.SaveChanges();
    }

    private static void AddSchool(AsmsDbContext context, Guid schoolId)
    {
        context.Schools.Add(new School { Id = schoolId, Name = $"School {schoolId}" });
        context.SaveChanges();
    }

    private static AcademicYear NewAcademicYear(Guid schoolId) => new()
    {
        SchoolId = schoolId,
        Name = $"Year {Guid.NewGuid()}"
    };

    private static Student NewStudent(Guid schoolId, string studentNumber) => new()
    {
        SchoolId = schoolId,
        StudentNumber = studentNumber,
        FirstName = "Test",
        LastName = "Student",
        FatherName = "Parent",
        DuplicateKey = Guid.NewGuid().ToString()
    };

    private static AttendanceRecord NewAttendance(
        Guid schoolId,
        Guid studentId,
        Guid classRoomId,
        DateOnly date) => new()
    {
        SchoolId = schoolId,
        StudentId = studentId,
        ClassRoomId = classRoomId,
        Date = date
    };

    private static StudentInvoice NewInvoice(
        Guid schoolId,
        Guid studentId,
        Guid academicYearId,
        string invoiceNumber) => new()
    {
        SchoolId = schoolId,
        StudentId = studentId,
        AcademicYearId = academicYearId,
        InvoiceNumber = invoiceNumber
    };

    private static Payment NewPayment(
        Guid schoolId,
        Guid studentId,
        Guid invoiceId,
        string receiptNumber) => new()
    {
        SchoolId = schoolId,
        StudentId = studentId,
        StudentInvoiceId = invoiceId,
        ReceiptNumber = receiptNumber
    };

    private static SyncOperation NewSyncOperation(
        Guid schoolId,
        Guid deviceId,
        Guid idempotencyKey) => new()
    {
        SchoolId = schoolId,
        DeviceId = deviceId,
        EntityType = "Student",
        PayloadJson = "{}",
        IdempotencyKey = idempotencyKey
    };

    private static SqliteConnection CreateOpenConnection()
    {
        var connection = new SqliteConnection("DataSource=:memory:");
        connection.Open();
        return connection;
    }

    private static AsmsDbContext CreateContext(SqliteConnection connection, Guid? schoolId)
    {
        var options = new DbContextOptionsBuilder<AsmsDbContext>()
            .UseSqlite(connection)
            .Options;
        var context = new AsmsDbContext(options, new TestTenant { SchoolId = schoolId });
        context.Database.EnsureCreated();
        return context;
    }
}
