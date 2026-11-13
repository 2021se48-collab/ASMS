using Asms.Domain.Schools;
using Asms.Domain.Students;
using Asms.Infrastructure.Persistence;
using Asms.Infrastructure.Tests.Persistence;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace Asms.Infrastructure.Tests.Migrations;

public class MigrationTests
{
    [Fact]
    public void Sqlite_migration_creates_expected_tables_and_is_applied()
    {
        using var connection = CreateOpenConnection();
        using var context = CreateSqliteContext(connection);

        context.Database.Migrate();

        Assert.Contains(
            context.Database.GetAppliedMigrations(),
            migration => migration.EndsWith("InitialCreate", StringComparison.Ordinal));

        var tables = GetSqliteTables(connection);
        Assert.Contains("Schools", tables);
        Assert.Contains("Students", tables);
        Assert.Contains("Payments", tables);
        Assert.Contains("AttendanceRecords", tables);
        Assert.Contains("AuditLogs", tables);
        Assert.Contains("SyncOperations", tables);
    }

    [Fact]
    public void Sqlite_migration_enforces_unique_student_number()
    {
        using var connection = CreateOpenConnection();
        var schoolId = Guid.NewGuid();
        using var context = CreateSqliteContext(connection, schoolId);
        context.Database.Migrate();
        context.Schools.Add(new School { Id = schoolId, Name = "Migration test school" });
        context.SaveChanges();

        context.Students.Add(NewStudent(schoolId, "ST-001"));
        context.SaveChanges();
        context.Students.Add(NewStudent(schoolId, "ST-001"));

        Assert.Throws<DbUpdateException>(() => context.SaveChanges());
    }

    [Fact]
    public void Sqlite_migration_has_no_pending_model_changes()
    {
        using var connection = CreateOpenConnection();
        using var context = CreateSqliteContext(connection);
        context.Database.Migrate();

        Assert.False(context.Database.HasPendingModelChanges());
    }

    [Fact]
    public void PostgreSql_migration_exists_and_has_no_pending_model_changes()
    {
        var options = new DbContextOptionsBuilder<AsmsDbContext>()
            .UseNpgsql(
                "Host=localhost;Database=asms_design;Username=design;Password=design",
                npgsql => npgsql.MigrationsAssembly("Asms.Infrastructure.Migrations.PostgreSql"))
            .Options;
        using var context = new AsmsDbContext(options, new TestTenant());

        Assert.Contains(
            context.Database.GetMigrations(),
            migration => migration.EndsWith("InitialCreate", StringComparison.Ordinal));
        Assert.False(context.Database.HasPendingModelChanges());
    }

    private static AsmsDbContext CreateSqliteContext(SqliteConnection connection, Guid? schoolId = null)
    {
        var options = new DbContextOptionsBuilder<AsmsDbContext>()
            .UseSqlite(
                connection,
                sqlite => sqlite.MigrationsAssembly("Asms.Infrastructure.Migrations.Sqlite"))
            .Options;
        return new AsmsDbContext(options, new TestTenant { SchoolId = schoolId });
    }

    private static SqliteConnection CreateOpenConnection()
    {
        var connection = new SqliteConnection("Data Source=:memory:");
        connection.Open();
        return connection;
    }

    private static List<string> GetSqliteTables(SqliteConnection connection)
    {
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT name FROM sqlite_master WHERE type = 'table'";
        using var reader = command.ExecuteReader();
        var tables = new List<string>();
        while (reader.Read())
        {
            tables.Add(reader.GetString(0));
        }

        return tables;
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
}
