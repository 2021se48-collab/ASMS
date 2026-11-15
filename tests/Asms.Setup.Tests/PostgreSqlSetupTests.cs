using Asms.Domain.Security;
using Asms.Infrastructure.Persistence;
using Asms.Setup;
using Microsoft.EntityFrameworkCore;
using Xunit.Sdk;

namespace Asms.Setup.Tests;

public sealed class PostgreSqlSetupTests
{
    [PostgresFact]
    public async Task Setup_creates_school_and_owner_roles_on_postgresql()
    {
        await using var database = await PostgresTestDatabase.CreateAsync();
        var request = new SetupRequest
        {
            Provider = "PostgreSql",
            ConnectionString = database.ConnectionString,
            SchoolName = $"School {Guid.NewGuid():N}",
            SchoolCode = $"P-{Guid.NewGuid():N}"[..10],
            DefaultLanguage = "en",
            OwnerFullName = $"Owner {Guid.NewGuid():N}",
            OwnerUserName = $"owner-{Guid.NewGuid():N}",
            OwnerPassword = CreatePassword()
        };

        var result = await new SetupService().RunAsync(request);

        Assert.True(result.Succeeded, result.ErrorCode);
        Assert.NotNull(result.SchoolId);
        var options = new DbContextOptionsBuilder<AsmsDbContext>()
            .UseNpgsql(
                database.ConnectionString,
                postgres => postgres.MigrationsAssembly("Asms.Infrastructure.Migrations.PostgreSql"))
            .Options;
        await using var db = new AsmsDbContext(options, new TestTenant(result.SchoolId));
        Assert.Equal(10, await db.AppRoles.IgnoreQueryFilters()
            .CountAsync(role => role.SchoolId == result.SchoolId));
        Assert.Single(await db.AppUsers.IgnoreQueryFilters()
            .Where(user => user.SchoolId == result.SchoolId)
            .ToListAsync());
        Assert.Single(await db.UserRoles.IgnoreQueryFilters()
            .Where(userRole => userRole.SchoolId == result.SchoolId)
            .ToListAsync());
        Assert.Contains(
            SystemRoles.SchoolOwner,
            await db.AppRoles.IgnoreQueryFilters()
                .Where(role => role.SchoolId == result.SchoolId)
                .Select(role => role.Name)
                .ToListAsync());
    }

    private static string CreatePassword() =>
        System.Security.Cryptography.RandomNumberGenerator.GetString(
            "abcdefghijklmnopqrstuvwxyz",
            16) + System.Security.Cryptography.RandomNumberGenerator.GetInt32(0, 10);

    private sealed class TestTenant(Guid? schoolId) : Asms.Application.Abstractions.ICurrentTenant
    {
        public Guid? SchoolId => schoolId;
        public Guid? UserId => null;
        public Guid? DeviceId => null;
    }

    private sealed class PostgresFactAttribute : FactAttribute
    {
        public PostgresFactAttribute()
        {
            if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("ASMS_TEST_POSTGRES")))
            {
                Skip = "ASMS_TEST_POSTGRES is not set";
            }
        }
    }
}
