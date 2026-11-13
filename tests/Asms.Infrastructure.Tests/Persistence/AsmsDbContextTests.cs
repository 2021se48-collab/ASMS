using Asms.Domain.Schools;
using Asms.Domain.Students;
using Asms.Infrastructure.Persistence;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace Asms.Infrastructure.Tests.Persistence;

public class AsmsDbContextTests
{
    [Fact]
    public void Tenant_cannot_see_another_tenants_students()
    {
        using var connection = CreateOpenConnection();
        var schoolA = Guid.NewGuid();
        var schoolB = Guid.NewGuid();
        using var contextA = CreateContext(connection, new TestTenant { SchoolId = schoolA });
        using var contextB = CreateContext(connection, new TestTenant { SchoolId = schoolB });

        contextA.Students.Add(new Student { FirstName = "A" });
        contextA.SaveChanges();
        contextB.Students.Add(new Student { FirstName = "B" });
        contextB.SaveChanges();

        Assert.Equal("A", contextA.Students.Single().FirstName);
        Assert.Equal("B", contextB.Students.Single().FirstName);
    }

    [Fact]
    public void Missing_tenant_returns_no_students()
    {
        using var connection = CreateOpenConnection();
        using var tenantContext = CreateContext(connection, new TestTenant { SchoolId = Guid.NewGuid() });
        tenantContext.Students.Add(new Student { FirstName = "Tenant student" });
        tenantContext.SaveChanges();

        using var noTenantContext = CreateContext(connection, new TestTenant());

        Assert.Empty(noTenantContext.Students.ToList());
    }

    [Fact]
    public void Soft_deleted_students_are_hidden_unless_filters_are_ignored()
    {
        using var connection = CreateOpenConnection();
        using var context = CreateContext(connection, new TestTenant { SchoolId = Guid.NewGuid() });
        var student = new Student { FirstName = "Soft deleted" };
        context.Students.Add(student);
        context.SaveChanges();

        student.IsDeleted = true;
        context.SaveChanges();

        Assert.Empty(context.Students.ToList());
        Assert.Single(context.Students.IgnoreQueryFilters().ToList());
    }

    [Fact]
    public void Added_entity_gets_the_current_tenant_school_id()
    {
        using var connection = CreateOpenConnection();
        var schoolId = Guid.NewGuid();
        using var context = CreateContext(connection, new TestTenant { SchoolId = schoolId });
        var student = new Student { FirstName = "New" };

        context.Students.Add(student);
        context.SaveChanges();

        Assert.Equal(schoolId, student.SchoolId);
    }

    [Fact]
    public void Saving_entity_for_another_school_throws()
    {
        using var connection = CreateOpenConnection();
        using var context = CreateContext(connection, new TestTenant { SchoolId = Guid.NewGuid() });
        context.Students.Add(new Student { SchoolId = Guid.NewGuid() });

        Assert.Throws<InvalidOperationException>(() => context.SaveChanges());
    }

    [Fact]
    public void Modifying_entity_for_another_school_throws()
    {
        using var connection = CreateOpenConnection();
        using var context = CreateContext(connection, new TestTenant { SchoolId = Guid.NewGuid() });
        var student = new Student();
        context.Students.Add(student);
        context.SaveChanges();
        student.SchoolId = Guid.NewGuid();
        student.FirstName = "Changed";

        Assert.Throws<InvalidOperationException>(() => context.SaveChanges());
    }

    [Fact]
    public void Modifying_entity_increments_version_and_updates_timestamp()
    {
        using var connection = CreateOpenConnection();
        using var context = CreateContext(connection, new TestTenant { SchoolId = Guid.NewGuid() });
        var student = new Student { FirstName = "Before" };
        context.Students.Add(student);
        context.SaveChanges();
        var oldTimestamp = new DateTime(2000, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        student.UpdatedAtUtc = oldTimestamp;
        student.FirstName = "After";

        context.SaveChanges();

        Assert.Equal(2, student.Version);
        Assert.NotEqual(oldTimestamp, student.UpdatedAtUtc);
    }

    [Fact]
    public void Hard_delete_throws()
    {
        using var connection = CreateOpenConnection();
        using var context = CreateContext(connection, new TestTenant { SchoolId = Guid.NewGuid() });
        var student = new Student { FirstName = "Keep" };
        context.Students.Add(student);
        context.SaveChanges();
        context.Students.Remove(student);

        Assert.Throws<InvalidOperationException>(() => context.SaveChanges());
    }

    [Fact]
    public void School_can_be_saved_and_soft_deleted_filter_applies()
    {
        using var connection = CreateOpenConnection();
        using var context = CreateContext(connection, new TestTenant());
        var school = new School { Name = "School" };
        context.Schools.Add(school);
        context.SaveChanges();

        Assert.Equal(school.Id, context.Schools.Single().Id);

        var oldTimestamp = new DateTime(2000, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        school.UpdatedAtUtc = oldTimestamp;
        school.IsDeleted = true;
        context.SaveChanges();

        Assert.Equal(2, school.Version);
        Assert.NotEqual(oldTimestamp, school.UpdatedAtUtc);
        Assert.Empty(context.Schools.ToList());
        Assert.Single(context.Schools.IgnoreQueryFilters().ToList());
    }

    [Fact]
    public void Hard_delete_school_throws()
    {
        using var connection = CreateOpenConnection();
        using var context = CreateContext(connection, new TestTenant());
        var school = new School { Name = "Keep" };
        context.Schools.Add(school);
        context.SaveChanges();
        context.Schools.Remove(school);

        Assert.Throws<InvalidOperationException>(() => context.SaveChanges());
    }

    [Fact]
    public void Added_entity_gets_user_and_device_stamps()
    {
        using var connection = CreateOpenConnection();
        var userId = Guid.NewGuid();
        var deviceId = Guid.NewGuid();
        using var context = CreateContext(connection, new TestTenant
        {
            SchoolId = Guid.NewGuid(),
            UserId = userId,
            DeviceId = deviceId
        });
        var student = new Student();

        context.Students.Add(student);
        context.SaveChanges();

        Assert.Equal(userId, student.CreatedByUserId);
        Assert.Equal(userId, student.UpdatedByUserId);
        Assert.Equal(deviceId, student.LastModifiedByDeviceId);
    }

    private static SqliteConnection CreateOpenConnection()
    {
        var connection = new SqliteConnection("DataSource=:memory:");
        connection.Open();
        return connection;
    }

    private static AsmsDbContext CreateContext(SqliteConnection connection, TestTenant tenant)
    {
        var options = new DbContextOptionsBuilder<AsmsDbContext>()
            .UseSqlite(connection)
            .Options;
        var context = new AsmsDbContext(options, tenant);
        context.Database.EnsureCreated();

        if (tenant.SchoolId is Guid schoolId &&
            !context.Schools.IgnoreQueryFilters().Any(school => school.Id == schoolId))
        {
            context.Schools.Add(new School { Id = schoolId, Name = "Test school" });
            context.SaveChanges();
        }

        return context;
    }
}
