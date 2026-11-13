using Asms.Domain.Academics;
using Asms.Domain.Auditing;
using Asms.Domain.Enums;
using Asms.Domain.Finance;
using Asms.Domain.Inventory;
using Asms.Domain.Schools;
using Asms.Domain.Students;
using Asms.Infrastructure.Persistence;
using Asms.Infrastructure.Tests.Persistence;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace Asms.Infrastructure.Tests.Migrations;

public class AppendOnlyTriggerTests
{
    [Fact]
    public void Payment_rows_are_append_only()
    {
        using var connection = CreateOpenConnection();
        var schoolId = Guid.NewGuid();
        using var context = CreateContext(connection, schoolId);
        context.Database.Migrate();
        var (_, student, invoice) = AddSchoolStudentAndInvoice(context, schoolId);
        var payment = Payment.CreatePayment(
            schoolId,
            invoice.Id,
            student.Id,
            125m,
            PaymentMethod.Cash,
            $"RCPT-{Guid.NewGuid():N}",
            DateTime.UtcNow,
            null);
        context.Payments.Add(payment);
        context.SaveChanges();

        Assert.Equal(125m, FindPayment(context, payment.Id).Amount);
        AssertAppendOnly(
            context,
            $"UPDATE \"Payments\" SET \"Amount\" = {250m} WHERE \"Id\" = {payment.Id}",
            $"DELETE FROM \"Payments\" WHERE \"Id\" = {payment.Id}",
            () => Assert.Equal(125m, FindPayment(context, payment.Id).Amount));
    }

    [Fact]
    public void Audit_log_rows_are_append_only()
    {
        using var connection = CreateOpenConnection();
        var schoolId = Guid.NewGuid();
        using var context = CreateContext(connection, schoolId);
        context.Database.Migrate();
        context.Schools.Add(new School { Id = schoolId, Name = "Audit test school" });
        var log = new AuditLog
        {
            SchoolId = schoolId,
            Action = "Created",
            EntityType = "Student",
            OccurredAtUtc = DateTime.UtcNow
        };
        context.AuditLogs.Add(log);
        context.SaveChanges();

        Assert.Equal("Created", FindAuditLog(context, log.Id).Action);
        AssertAppendOnly(
            context,
            $"UPDATE \"AuditLogs\" SET \"Action\" = {"Changed"} WHERE \"Id\" = {log.Id}",
            $"DELETE FROM \"AuditLogs\" WHERE \"Id\" = {log.Id}",
            () => Assert.Equal("Created", FindAuditLog(context, log.Id).Action));
    }

    [Fact]
    public void Inventory_movement_rows_are_append_only()
    {
        using var connection = CreateOpenConnection();
        var schoolId = Guid.NewGuid();
        using var context = CreateContext(connection, schoolId);
        context.Database.Migrate();
        context.Schools.Add(new School { Id = schoolId, Name = "Inventory test school" });
        var item = new InventoryItem { SchoolId = schoolId, Name = "Desk" };
        context.InventoryItems.Add(item);
        context.SaveChanges();
        var movement = InventoryMovement.Create(
            schoolId,
            item.Id,
            InventoryMovementType.StockIn,
            3,
            "Received stock",
            DateTime.UtcNow,
            null);
        context.InventoryMovements.Add(movement);
        context.SaveChanges();

        Assert.Equal("Received stock", FindInventoryMovement(context, movement.Id).Reason);
        AssertAppendOnly(
            context,
            $"UPDATE \"InventoryMovements\" SET \"Reason\" = {"Changed"} WHERE \"Id\" = {movement.Id}",
            $"DELETE FROM \"InventoryMovements\" WHERE \"Id\" = {movement.Id}",
            () => Assert.Equal("Received stock", FindInventoryMovement(context, movement.Id).Reason));
    }

    [Fact]
    public void Student_rows_can_still_be_updated_and_soft_deleted()
    {
        using var connection = CreateOpenConnection();
        var schoolId = Guid.NewGuid();
        using var context = CreateContext(connection, schoolId);
        context.Database.Migrate();
        context.Schools.Add(new School { Id = schoolId, Name = "Student test school" });
        var student = NewStudent(schoolId);
        context.Students.Add(student);
        context.SaveChanges();

        student.FirstName = "Updated";
        context.SaveChanges();
        Assert.Equal("Updated", context.Students.Single(item => item.Id == student.Id).FirstName);

        student.IsDeleted = true;
        context.SaveChanges();
        Assert.Empty(context.Students.ToList());
        Assert.Equal(
            "Updated",
            context.Students.IgnoreQueryFilters().Single(item => item.Id == student.Id).FirstName);
    }

    [Fact]
    public void SQLite_migrations_are_ordered_and_model_has_no_pending_changes()
    {
        using var connection = CreateOpenConnection();
        using var context = CreateContext(connection, Guid.NewGuid());
        context.Database.Migrate();
        var migrations = context.Database.GetAppliedMigrations().ToArray();

        Assert.Equal(2, migrations.Length);
        Assert.EndsWith("InitialCreate", migrations[0]);
        Assert.EndsWith("AddAppendOnlyTriggers", migrations[1]);
        Assert.False(context.Database.HasPendingModelChanges());
    }

    [Fact]
    public void Trigger_migration_down_removes_triggers_and_forward_migration_restores_them()
    {
        using var connection = CreateOpenConnection();
        using var context = CreateContext(connection);
        context.Database.Migrate();
        var initialMigration = context.Database.GetMigrations()
            .Single(migration => migration.EndsWith("InitialCreate", StringComparison.Ordinal));
        var migrator = context.GetService<IMigrator>();

        migrator.Migrate(initialMigration);
        Assert.Empty(GetTriggerNames(connection));

        migrator.Migrate();
        Assert.Equal(
            ExpectedSqliteTriggers().OrderBy(name => name),
            GetTriggerNames(connection).OrderBy(name => name));
    }

    [Fact]
    public void PostgreSql_migration_is_ordered_and_contains_shared_trigger_function()
    {
        var options = new DbContextOptionsBuilder<AsmsDbContext>()
            .UseNpgsql(
                "Host=localhost;Database=asms_design;Username=design;Password=design",
                npgsql => npgsql.MigrationsAssembly("Asms.Infrastructure.Migrations.PostgreSql"))
            .Options;
        using var context = new AsmsDbContext(options, new TestTenant());
        var migrations = context.Database.GetMigrations().ToArray();

        Assert.Equal(2, migrations.Length);
        Assert.EndsWith("InitialCreate", migrations[0]);
        Assert.EndsWith("AddAppendOnlyTriggers", migrations[1]);

        // Real PostgreSQL trigger behavior is not tested yet.
        var sourcePath = Path.Combine(
            FindRepositoryRoot(),
            "src",
            "Asms.Infrastructure.Migrations.PostgreSql",
            "Migrations",
            "20261113102522_AddAppendOnlyTriggers.cs");
        var source = File.ReadAllText(sourcePath);
        Assert.True(source.Contains("asms_block_changes", StringComparison.Ordinal));
    }

    private static void AssertAppendOnly(
        AsmsDbContext context,
        FormattableString update,
        FormattableString delete,
        Action assertUnchanged)
    {
        var updateException = Assert.Throws<SqliteException>(
            () => context.Database.ExecuteSqlInterpolated(update));
        Assert.Contains("APPEND_ONLY_TABLE", updateException.Message);
        assertUnchanged();

        var deleteException = Assert.Throws<SqliteException>(
            () => context.Database.ExecuteSqlInterpolated(delete));
        Assert.Contains("APPEND_ONLY_TABLE", deleteException.Message);
        assertUnchanged();
    }

    private static Payment FindPayment(AsmsDbContext context, Guid id) =>
        context.Payments.IgnoreQueryFilters().AsNoTracking().Single(payment => payment.Id == id);

    private static AuditLog FindAuditLog(AsmsDbContext context, Guid id) =>
        context.AuditLogs.IgnoreQueryFilters().AsNoTracking().Single(log => log.Id == id);

    private static InventoryMovement FindInventoryMovement(AsmsDbContext context, Guid id) =>
        context.InventoryMovements.IgnoreQueryFilters().AsNoTracking()
            .Single(movement => movement.Id == id);

    private static (School School, Student Student, StudentInvoice Invoice)
        AddSchoolStudentAndInvoice(AsmsDbContext context, Guid schoolId)
    {
        var school = new School { Id = schoolId, Name = "Payment test school" };
        var academicYear = new AcademicYear
        {
            SchoolId = schoolId,
            Name = $"Year {Guid.NewGuid()}"
        };
        var student = NewStudent(schoolId);
        context.AddRange(school, academicYear, student);
        context.SaveChanges();

        var invoice = new StudentInvoice
        {
            SchoolId = schoolId,
            StudentId = student.Id,
            AcademicYearId = academicYear.Id,
            InvoiceNumber = $"INV-{Guid.NewGuid():N}"
        };
        context.StudentInvoices.Add(invoice);
        context.SaveChanges();
        return (school, student, invoice);
    }

    private static Student NewStudent(Guid schoolId) => new()
    {
        SchoolId = schoolId,
        StudentNumber = $"ST-{Guid.NewGuid():N}",
        FirstName = "Test",
        LastName = "Student",
        FatherName = "Parent",
        DuplicateKey = Guid.NewGuid().ToString()
    };

    private static SqliteConnection CreateOpenConnection()
    {
        var connection = new SqliteConnection("Data Source=:memory:");
        connection.Open();
        return connection;
    }

    private static AsmsDbContext CreateContext(SqliteConnection connection, Guid? schoolId = null)
    {
        var options = new DbContextOptionsBuilder<AsmsDbContext>()
            .UseSqlite(
                connection,
                sqlite => sqlite.MigrationsAssembly("Asms.Infrastructure.Migrations.Sqlite"))
            .Options;
        return new AsmsDbContext(options, new TestTenant { SchoolId = schoolId });
    }

    private static List<string> GetTriggerNames(SqliteConnection connection)
    {
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT name FROM sqlite_master WHERE type = 'trigger'";
        using var reader = command.ExecuteReader();
        var names = new List<string>();
        while (reader.Read())
        {
            names.Add(reader.GetString(0));
        }

        return names;
    }

    private static string[] ExpectedSqliteTriggers() =>
    [
        "TR_Payments_NoUpdate",
        "TR_Payments_NoDelete",
        "TR_AuditLogs_NoUpdate",
        "TR_AuditLogs_NoDelete",
        "TR_InventoryMovements_NoUpdate",
        "TR_InventoryMovements_NoDelete"
    ];

    private static string FindRepositoryRoot()
    {
        DirectoryInfo? directory = new(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "ASMS.slnx")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException("Could not find the ASMS solution directory.");
    }
}
