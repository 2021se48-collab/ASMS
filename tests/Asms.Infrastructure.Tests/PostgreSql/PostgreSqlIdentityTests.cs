using Asms.Infrastructure.Identity;
using Asms.Infrastructure.Persistence;
using Asms.Infrastructure.Tests.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;

namespace Asms.Infrastructure.Tests.PostgreSql;

public class PostgreSqlIdentityTests
{
    [PostgresFact]
    public async Task Identity_migration_applies_and_creates_expected_tables()
    {
        await using var database = await PostgresTestDatabase.CreateAsync();
        await using var context = CreateIdentityContext(database);

        await context.Database.MigrateAsync();

        var applied = context.Database.GetAppliedMigrations().ToArray();
        Assert.Single(applied);
        Assert.EndsWith("IdentityInitial", applied[0], StringComparison.Ordinal);
        Assert.False(context.Database.HasPendingModelChanges());

        var tables = await context.Database
            .SqlQueryRaw<string>(
                "SELECT table_name AS \"Value\" FROM information_schema.tables WHERE table_schema = 'public'")
            .ToListAsync();
        Assert.Contains("Identity_Users", tables);
        Assert.Contains("Identity_UserClaims", tables);
        Assert.Contains("Identity_UserLogins", tables);
        Assert.Contains("Identity_UserTokens", tables);
    }

    [PostgresFact]
    public async Task Normalized_email_is_unique_per_school_only()
    {
        await using var database = await PostgresTestDatabase.CreateAsync();
        await using var context = CreateIdentityContext(database);
        await context.Database.MigrateAsync();
        var schoolA = Guid.NewGuid();
        var email = $"{Guid.NewGuid():N}@example.test";

        context.Users.Add(NewUser(schoolA, email));
        await context.SaveChangesAsync();

        context.Users.Add(NewUser(schoolA, email));
        var duplicate = await Assert.ThrowsAsync<DbUpdateException>(
            () => context.SaveChangesAsync());
        Assert.Equal(PostgresErrorCodes.UniqueViolation, Assert.IsType<PostgresException>(duplicate.InnerException).SqlState);

        context.ChangeTracker.Clear();
        context.Users.Add(NewUser(Guid.NewGuid(), email));
        await context.SaveChangesAsync();
    }

    [PostgresFact]
    public async Task Identity_lockout_works_on_postgresql()
    {
        await using var database = await PostgresTestDatabase.CreateAsync();
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddAsmsIdentity(database.ConfigureIdentityDbContext);
        using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AsmsIdentityDbContext>();
        await context.Database.MigrateAsync();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<AsmsIdentityUser>>();
        var user = NewUser(Guid.NewGuid(), $"{Guid.NewGuid():N}@example.test");

        Assert.True((await users.CreateAsync(user, CreateValidPassword())).Succeeded);
        for (var attempt = 0; attempt < 5; attempt++)
        {
            Assert.True((await users.AccessFailedAsync(user)).Succeeded);
        }

        Assert.True(await users.IsLockedOutAsync(user));
        Assert.True((await users.AccessFailedAsync(user)).Succeeded);
        Assert.True(await users.IsLockedOutAsync(user));
    }

    [PostgresFact]
    public async Task Main_and_identity_contexts_migrate_into_the_same_database()
    {
        await using var database = await PostgresTestDatabase.CreateAsync();
        await using var mainContext = database.CreateContext(new TestTenant());
        await using var identityContext = CreateIdentityContext(database);

        await mainContext.Database.MigrateAsync();
        await identityContext.Database.MigrateAsync();

        var tables = await identityContext.Database
            .SqlQueryRaw<string>(
                "SELECT table_name AS \"Value\" FROM information_schema.tables WHERE table_schema = 'public'")
            .ToListAsync();
        Assert.Contains("Schools", tables);
        Assert.Contains("Identity_Users", tables);
        Assert.Contains("Identity_UserClaims", tables);
        Assert.Contains("Identity_UserLogins", tables);
        Assert.Contains("Identity_UserTokens", tables);
    }

    private static AsmsIdentityDbContext CreateIdentityContext(PostgresTestDatabase database)
    {
        var options = new DbContextOptionsBuilder<AsmsIdentityDbContext>();
        database.ConfigureIdentityDbContext(options);
        return new AsmsIdentityDbContext(options.Options);
    }

    private static AsmsIdentityUser NewUser(Guid schoolId, string email)
    {
        var id = Guid.NewGuid().ToString("N");
        return new AsmsIdentityUser
        {
            Id = Guid.NewGuid(),
            UserName = $"user-{id}",
            NormalizedUserName = $"USER-{id}",
            Email = email,
            NormalizedEmail = email.ToUpperInvariant(),
            SchoolId = schoolId
        };
    }

    private static string CreateValidPassword() => $"Aa1{Guid.NewGuid():N}";
}
