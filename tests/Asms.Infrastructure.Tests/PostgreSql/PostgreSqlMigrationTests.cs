using System.Globalization;
using Asms.Domain.Academics;
using Asms.Domain.Auditing;
using Asms.Domain.Enums;
using Asms.Domain.Finance;
using Asms.Domain.Inventory;
using Asms.Domain.Schools;
using Asms.Domain.Security;
using Asms.Domain.Students;
using Asms.Infrastructure.Persistence;
using Asms.Infrastructure.Persistence.Seeding;
using Asms.Infrastructure.Tests.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql;
using Xunit;

namespace Asms.Infrastructure.Tests.PostgreSql;

public class PostgreSqlMigrationTests
{
    [PostgresFact]
    public async Task Migrate_applies_initial_and_trigger_migrations_in_order()
    {
        await using var database = await PostgresTestDatabase.CreateAsync();
        await using var context = database.CreateContext(new TestTenant());

        await context.Database.MigrateAsync();
        var applied = context.Database.GetAppliedMigrations().ToArray();

        Assert.Equal(2, applied.Length);
        Assert.EndsWith("InitialCreate", applied[0]);
        Assert.EndsWith("AddAppendOnlyTriggers", applied[1]);
    }

    [PostgresFact]
    public async Task Model_has_no_pending_changes()
    {
        await using var database = await PostgresTestDatabase.CreateAsync();
        await using var context = database.CreateContext(new TestTenant());

        await context.Database.MigrateAsync();

        Assert.False(context.Database.HasPendingModelChanges());
    }

    [PostgresFact]
    public async Task Migration_creates_expected_tables()
    {
        await using var database = await PostgresTestDatabase.CreateAsync();
        await using var context = database.CreateContext(new TestTenant());
        await context.Database.MigrateAsync();

        var tables = await GetPublicTablesAsync(context);
        Assert.Contains("Schools", tables);
        Assert.Contains("Students", tables);
        Assert.Contains("Payments", tables);
        Assert.Contains("AttendanceRecords", tables);
        Assert.Contains("AuditLogs", tables);
        Assert.Contains("SyncOperations", tables);
        Assert.Contains("InventoryMovements", tables);
    }

    [PostgresFact]
    public async Task Student_number_is_unique_per_school_only()
    {
        await using var database = await PostgresTestDatabase.CreateAsync();
        var schoolA = Guid.NewGuid();
        var schoolB = Guid.NewGuid();

        await using (var contextA = database.CreateContext(new TestTenant { SchoolId = schoolA }))
        {
            await contextA.Database.MigrateAsync();
            contextA.Schools.AddRange(
                new School { Id = schoolA, Name = "School A" },
                new School { Id = schoolB, Name = "School B" });
            await contextA.SaveChangesAsync();

            contextA.Students.Add(NewStudent(schoolA, "ST-001"));
            await contextA.SaveChangesAsync();
            contextA.Students.Add(NewStudent(schoolA, "ST-001"));
            var duplicate = await Assert.ThrowsAsync<DbUpdateException>(
                () => contextA.SaveChangesAsync());
            AssertSqlState(duplicate, PostgresErrorCodes.UniqueViolation);
        }

        await using var contextB = database.CreateContext(new TestTenant { SchoolId = schoolB });
        contextB.Students.Add(NewStudent(schoolB, "ST-001"));
        await contextB.SaveChangesAsync();
        Assert.Single(await contextB.Students.ToListAsync());
    }

    [PostgresFact]
    public async Task Enrollment_with_missing_student_fails_foreign_key_constraint()
    {
        await using var database = await PostgresTestDatabase.CreateAsync();
        var schoolId = Guid.NewGuid();
        await using var context = database.CreateContext(new TestTenant { SchoolId = schoolId });
        await context.Database.MigrateAsync();

        var school = new School { Id = schoolId, Name = "Enrollment school" };
        var academicYear = new AcademicYear { SchoolId = schoolId, Name = "2026" };
        var grade = new Grade { SchoolId = schoolId, Name = "Grade 1" };
        context.AddRange(school, academicYear, grade);
        await context.SaveChangesAsync();
        var classRoom = new ClassRoom
        {
            SchoolId = schoolId,
            AcademicYearId = academicYear.Id,
            GradeId = grade.Id,
            Name = "Room A"
        };
        context.ClassRooms.Add(classRoom);
        await context.SaveChangesAsync();

        context.Enrollments.Add(new Enrollment
        {
            SchoolId = schoolId,
            StudentId = Guid.NewGuid(),
            AcademicYearId = academicYear.Id,
            ClassRoomId = classRoom.Id
        });
        var exception = await Assert.ThrowsAsync<DbUpdateException>(
            () => context.SaveChangesAsync());

        AssertSqlState(exception, PostgresErrorCodes.ForeignKeyViolation);
    }

    [PostgresFact]
    public async Task Payment_precision_and_database_sum_are_exact()
    {
        await using var database = await PostgresTestDatabase.CreateAsync();
        var schoolId = Guid.NewGuid();
        await using var context = database.CreateContext(new TestTenant { SchoolId = schoolId });
        await context.Database.MigrateAsync();
        var (_, student, invoice) = await AddSchoolStudentAndInvoiceAsync(context, schoolId);

        var firstPayment = NewPayment(schoolId, invoice.Id, student.Id, "RCPT-001", 1234.56m);
        var secondPayment = NewPayment(schoolId, invoice.Id, student.Id, "RCPT-002", 0.44m);
        context.Payments.AddRange(firstPayment, secondPayment);
        await context.SaveChangesAsync();

        var storedAmount = await context.Payments.AsNoTracking()
            .Where(payment => payment.Id == firstPayment.Id)
            .Select(payment => payment.Amount)
            .SingleAsync();
        var total = await context.Payments
            .Where(payment => payment.SchoolId == schoolId)
            .SumAsync(payment => payment.Amount);

        Assert.Equal(1234.56m, storedAmount);
        Assert.Equal(1235.00m, total);
    }

    [PostgresFact]
    public async Task Payment_rows_reject_updates_and_deletes()
    {
        await using var database = await PostgresTestDatabase.CreateAsync();
        var schoolId = Guid.NewGuid();
        await using var context = database.CreateContext(new TestTenant { SchoolId = schoolId });
        await context.Database.MigrateAsync();
        var (_, student, invoice) = await AddSchoolStudentAndInvoiceAsync(context, schoolId);
        var payment = NewPayment(schoolId, invoice.Id, student.Id, "RCPT-001", 125m);
        context.Payments.Add(payment);
        await context.SaveChangesAsync();

        await AssertAppendOnlyAsync(
            context,
            $"UPDATE \"Payments\" SET \"Amount\" = {250m} WHERE \"Id\" = {payment.Id}",
            $"DELETE FROM \"Payments\" WHERE \"Id\" = {payment.Id}",
            async () => Assert.Equal(125m, (await FindPaymentAsync(context, payment.Id)).Amount));
    }

    [PostgresFact]
    public async Task Audit_log_rows_reject_updates_and_deletes()
    {
        await using var database = await PostgresTestDatabase.CreateAsync();
        var schoolId = Guid.NewGuid();
        await using var context = database.CreateContext(new TestTenant { SchoolId = schoolId });
        await context.Database.MigrateAsync();
        await AddSchoolAsync(context, schoolId);
        var auditLog = new AuditLog
        {
            SchoolId = schoolId,
            Action = "Created",
            EntityType = "Student",
            OccurredAtUtc = DateTime.UtcNow
        };
        context.AuditLogs.Add(auditLog);
        await context.SaveChangesAsync();

        await AssertAppendOnlyAsync(
            context,
            $"UPDATE \"AuditLogs\" SET \"Action\" = {"Changed"} WHERE \"Id\" = {auditLog.Id}",
            $"DELETE FROM \"AuditLogs\" WHERE \"Id\" = {auditLog.Id}",
            async () => Assert.Equal("Created", (await FindAuditLogAsync(context, auditLog.Id)).Action));
    }

    [PostgresFact]
    public async Task Inventory_movement_rows_reject_updates_and_deletes()
    {
        await using var database = await PostgresTestDatabase.CreateAsync();
        var schoolId = Guid.NewGuid();
        await using var context = database.CreateContext(new TestTenant { SchoolId = schoolId });
        await context.Database.MigrateAsync();
        await AddSchoolAsync(context, schoolId);
        var item = new InventoryItem { SchoolId = schoolId, Name = "Desk" };
        context.InventoryItems.Add(item);
        await context.SaveChangesAsync();
        var movement = InventoryMovement.Create(
            schoolId,
            item.Id,
            InventoryMovementType.StockIn,
            3,
            "Received stock",
            DateTime.UtcNow,
            null);
        context.InventoryMovements.Add(movement);
        await context.SaveChangesAsync();

        await AssertAppendOnlyAsync(
            context,
            $"UPDATE \"InventoryMovements\" SET \"Reason\" = {"Changed"} WHERE \"Id\" = {movement.Id}",
            $"DELETE FROM \"InventoryMovements\" WHERE \"Id\" = {movement.Id}",
            async () => Assert.Equal(
                "Received stock",
                (await FindInventoryMovementAsync(context, movement.Id)).Reason));
    }

    [PostgresFact]
    public async Task Student_can_still_be_updated_and_soft_deleted()
    {
        await using var database = await PostgresTestDatabase.CreateAsync();
        var schoolId = Guid.NewGuid();
        await using var context = database.CreateContext(new TestTenant { SchoolId = schoolId });
        await context.Database.MigrateAsync();
        await AddSchoolAsync(context, schoolId);
        var student = NewStudent(schoolId, "ST-001");
        context.Students.Add(student);
        await context.SaveChangesAsync();

        student.FirstName = "Updated";
        await context.SaveChangesAsync();
        Assert.Equal(
            "Updated",
            (await context.Students.SingleAsync(item => item.Id == student.Id)).FirstName);

        student.IsDeleted = true;
        await context.SaveChangesAsync();
        Assert.Empty(await context.Students.ToListAsync());
        Assert.Equal(
            "Updated",
            (await context.Students.IgnoreQueryFilters()
                .SingleAsync(item => item.Id == student.Id)).FirstName);
    }

    [PostgresFact]
    public async Task Down_removes_triggers_and_function_and_forward_migration_restores_them()
    {
        await using var database = await PostgresTestDatabase.CreateAsync();
        await using var context = database.CreateContext(new TestTenant());
        await context.Database.MigrateAsync();
        var initialMigration = context.Database.GetMigrations()
            .Single(migration => migration.EndsWith("InitialCreate", StringComparison.Ordinal));
        var migrator = context.GetService<IMigrator>();

        await migrator.MigrateAsync(initialMigration);
        Assert.Equal(0, await GetTriggerCountAsync(context));
        Assert.Equal(0, await GetFunctionCountAsync(context));

        await migrator.MigrateAsync();
        Assert.Equal(3, await GetTriggerCountAsync(context));
        Assert.Equal(1, await GetFunctionCountAsync(context));
    }

    [PostgresFact]
    public async Task Tenants_only_see_their_own_students()
    {
        await using var database = await PostgresTestDatabase.CreateAsync();
        var schoolA = Guid.NewGuid();
        var schoolB = Guid.NewGuid();
        await using (var contextA = database.CreateContext(new TestTenant { SchoolId = schoolA }))
        {
            await contextA.Database.MigrateAsync();
            contextA.Schools.AddRange(
                new School { Id = schoolA, Name = "School A" },
                new School { Id = schoolB, Name = "School B" });
            contextA.Students.Add(NewStudent(schoolA, "ST-A"));
            await contextA.SaveChangesAsync();
        }

        await using (var contextB = database.CreateContext(new TestTenant { SchoolId = schoolB }))
        {
            contextB.Students.Add(NewStudent(schoolB, "ST-B"));
            await contextB.SaveChangesAsync();
            Assert.Equal("ST-B", (await contextB.Students.SingleAsync()).StudentNumber);
        }

        await using var noTenantContext = database.CreateContext(new TestTenant());
        Assert.Empty(await noTenantContext.Students.ToListAsync());
    }

    [PostgresFact]
    public async Task School_role_seeder_creates_exact_permissions_and_is_idempotent()
    {
        await using var database = await PostgresTestDatabase.CreateAsync();
        var schoolId = Guid.NewGuid();
        await using var context = database.CreateContext(new TestTenant { SchoolId = schoolId });
        await context.Database.MigrateAsync();
        await AddSchoolAsync(context, schoolId);
        var seeder = new SchoolRoleSeeder(context, new TestTenant { SchoolId = schoolId });

        var firstResult = await seeder.SeedAsync(schoolId);
        var roles = await context.AppRoles.IgnoreQueryFilters()
            .Where(role => role.SchoolId == schoolId)
            .ToListAsync();
        var permissions = await context.RolePermissions.IgnoreQueryFilters()
            .Where(permission => permission.SchoolId == schoolId)
            .ToListAsync();
        var expectedPermissionCount = DefaultRolePermissions.SchoolRoles
            .Sum(role => DefaultRolePermissions.ForRole(role).Count);

        Assert.Equal(10, firstResult.RolesAdded);
        Assert.Equal(expectedPermissionCount, firstResult.PermissionsAdded);
        Assert.Equal(10, roles.Count);
        Assert.Equal(expectedPermissionCount, permissions.Count);
        Assert.All(roles, role =>
        {
            var actual = permissions
                .Where(permission => permission.RoleId == role.Id)
                .Select(permission => permission.Permission)
                .OrderBy(permission => permission);
            var expected = DefaultRolePermissions.ForRole(role.Name)
                .OrderBy(permission => permission);
            Assert.Equal(expected, actual);
        });

        var secondResult = await seeder.SeedAsync(schoolId);
        Assert.Equal(new SeedResult(0, 0, 0, 0), secondResult);
    }

    private static void AssertSqlState(DbUpdateException exception, string expectedState)
    {
        var postgresException = Assert.IsType<PostgresException>(exception.InnerException);
        Assert.Equal(expectedState, postgresException.SqlState);
    }

    private static async Task AssertAppendOnlyAsync(
        AsmsDbContext context,
        FormattableString update,
        FormattableString delete,
        Func<Task> assertUnchanged)
    {
        var updateException = await Assert.ThrowsAsync<PostgresException>(
            () => context.Database.ExecuteSqlInterpolatedAsync(update));
        Assert.Contains("APPEND_ONLY_TABLE", updateException.Message);
        await assertUnchanged();

        var deleteException = await Assert.ThrowsAsync<PostgresException>(
            () => context.Database.ExecuteSqlInterpolatedAsync(delete));
        Assert.Contains("APPEND_ONLY_TABLE", deleteException.Message);
        await assertUnchanged();
    }

    private static async Task<Payment> FindPaymentAsync(AsmsDbContext context, Guid id) =>
        await context.Payments.IgnoreQueryFilters().AsNoTracking()
            .SingleAsync(payment => payment.Id == id);

    private static async Task<AuditLog> FindAuditLogAsync(AsmsDbContext context, Guid id) =>
        await context.AuditLogs.IgnoreQueryFilters().AsNoTracking()
            .SingleAsync(log => log.Id == id);

    private static async Task<InventoryMovement> FindInventoryMovementAsync(
        AsmsDbContext context,
        Guid id) =>
        await context.InventoryMovements.IgnoreQueryFilters().AsNoTracking()
            .SingleAsync(movement => movement.Id == id);

    private static async Task<(School School, Student Student, StudentInvoice Invoice)>
        AddSchoolStudentAndInvoiceAsync(AsmsDbContext context, Guid schoolId)
    {
        var school = new School { Id = schoolId, Name = "Payment test school" };
        var academicYear = new AcademicYear
        {
            SchoolId = schoolId,
            Name = $"Year {Guid.NewGuid()}"
        };
        var student = NewStudent(schoolId, $"ST-{Guid.NewGuid():N}");
        context.AddRange(school, academicYear, student);
        await context.SaveChangesAsync();

        var invoice = new StudentInvoice
        {
            SchoolId = schoolId,
            StudentId = student.Id,
            AcademicYearId = academicYear.Id,
            InvoiceNumber = $"INV-{Guid.NewGuid():N}"
        };
        context.StudentInvoices.Add(invoice);
        await context.SaveChangesAsync();
        return (school, student, invoice);
    }

    private static async Task AddSchoolAsync(AsmsDbContext context, Guid schoolId)
    {
        context.Schools.Add(new School { Id = schoolId, Name = $"School {schoolId}" });
        await context.SaveChangesAsync();
    }

    private static Student NewStudent(Guid schoolId, string studentNumber) => new()
    {
        SchoolId = schoolId,
        StudentNumber = studentNumber,
        FirstName = "Test",
        LastName = "Student",
        FatherName = "Parent",
        DuplicateKey = Guid.NewGuid().ToString()
    };

    private static Payment NewPayment(
        Guid schoolId,
        Guid invoiceId,
        Guid studentId,
        string receiptNumber,
        decimal amount) => Payment.CreatePayment(
            schoolId,
            invoiceId,
            studentId,
            amount,
            PaymentMethod.Cash,
            receiptNumber,
            DateTime.UtcNow,
            null);

    private static async Task<List<string>> GetPublicTablesAsync(AsmsDbContext context)
    {
        await context.Database.OpenConnectionAsync();
        try
        {
            await using var command = context.Database.GetDbConnection().CreateCommand();
            command.CommandText = """
                SELECT table_name
                FROM information_schema.tables
                WHERE table_schema = 'public'
                """;
            await using var reader = await command.ExecuteReaderAsync();
            var tables = new List<string>();
            while (await reader.ReadAsync())
            {
                tables.Add(reader.GetString(0));
            }

            return tables;
        }
        finally
        {
            await context.Database.CloseConnectionAsync();
        }
    }

    private static async Task<int> GetTriggerCountAsync(AsmsDbContext context) =>
        await ExecuteCountAsync(
            context,
            """
            SELECT COUNT(*)
            FROM pg_trigger AS t
            JOIN pg_class AS c ON c.oid = t.tgrelid
            JOIN pg_namespace AS n ON n.oid = c.relnamespace
            WHERE n.nspname = 'public'
              AND NOT t.tgisinternal
              AND t.tgname IN (
                  'TR_Payments_NoChange',
                  'TR_AuditLogs_NoChange',
                  'TR_InventoryMovements_NoChange')
            """);

    private static async Task<int> GetFunctionCountAsync(AsmsDbContext context) =>
        await ExecuteCountAsync(
            context,
            """
            SELECT COUNT(*)
            FROM pg_proc AS p
            JOIN pg_namespace AS n ON n.oid = p.pronamespace
            WHERE n.nspname = 'public'
              AND p.proname = 'asms_block_changes'
            """);

    private static async Task<int> ExecuteCountAsync(AsmsDbContext context, string sql)
    {
        await context.Database.OpenConnectionAsync();
        try
        {
            await using var command = context.Database.GetDbConnection().CreateCommand();
            command.CommandText = sql;
            var value = await command.ExecuteScalarAsync();
            return Convert.ToInt32(value, CultureInfo.InvariantCulture);
        }
        finally
        {
            await context.Database.CloseConnectionAsync();
        }
    }
}

public sealed class PostgresFactAttribute : FactAttribute
{
    public PostgresFactAttribute()
    {
        if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("ASMS_TEST_POSTGRES")))
        {
            Skip = "ASMS_TEST_POSTGRES is not set";
        }
    }
}
