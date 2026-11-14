using Asms.Infrastructure.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Asms.Infrastructure.Tests.Identity;

public class IdentityStoreTests
{
    [Fact]
    public async Task User_passwords_are_hashed_with_distinct_salts()
    {
        using var store = new SqliteIdentityStore();
        using var scope = store.Services.CreateScope();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<AsmsIdentityUser>>();
        var password = CreateValidPassword();
        var first = NewUser();
        var second = NewUser();

        Assert.True((await users.CreateAsync(first, password)).Succeeded);
        Assert.True((await users.CreateAsync(second, password)).Succeeded);

        var firstHash = (await users.FindByIdAsync(first.Id.ToString()))!.PasswordHash;
        var secondHash = (await users.FindByIdAsync(second.Id.ToString()))!.PasswordHash;
        Assert.NotNull(firstHash);
        Assert.NotNull(secondHash);
        Assert.False(firstHash.Contains(password, StringComparison.Ordinal));
        Assert.False(firstHash == secondHash);
    }

    [Fact]
    public async Task Password_policy_rejects_each_invalid_password()
    {
        using var store = new SqliteIdentityStore();
        using var scope = store.Services.CreateScope();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<AsmsIdentityUser>>();
        var invalidPasswords = new[]
        {
            (new string(['A', 'a', '1', 'A', 'a', '1', 'A']), "PasswordTooShort"),
            (new string(['A', 'b', 'C', 'd', 'E', 'f', 'G', 'h']), "PasswordRequiresDigit"),
            (new string(['A', 'B', 'C', 'D', 'E', 'F', 'G', '1']), "PasswordRequiresLower"),
            (new string(['A', 'a', '1', 'A', 'a', '1', 'A', 'a']), "PasswordRequiresUniqueChars")
        };

        foreach (var (password, expectedError) in invalidPasswords)
        {
            var result = await users.CreateAsync(NewUser(), password);
            Assert.False(result.Succeeded);
            Assert.Contains(result.Errors, error => error.Code == expectedError);
        }
    }

    [Fact]
    public async Task Password_check_accepts_only_the_correct_password()
    {
        using var store = new SqliteIdentityStore();
        using var scope = store.Services.CreateScope();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<AsmsIdentityUser>>();
        var user = NewUser();
        var password = CreateValidPassword();

        Assert.True((await users.CreateAsync(user, password)).Succeeded);
        Assert.True(await users.CheckPasswordAsync(user, password));
        Assert.False(await users.CheckPasswordAsync(user, CreateValidPassword()));
    }

    [Fact]
    public async Task Five_failed_attempts_lock_the_user()
    {
        using var store = new SqliteIdentityStore();
        using var scope = store.Services.CreateScope();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<AsmsIdentityUser>>();
        var user = NewUser();

        Assert.True((await users.CreateAsync(user, CreateValidPassword())).Succeeded);

        for (var attempt = 0; attempt < 5; attempt++)
        {
            Assert.True((await users.AccessFailedAsync(user)).Succeeded);
        }

        Assert.True(await users.IsLockedOutAsync(user));
        Assert.True((await users.AccessFailedAsync(user)).Succeeded);
        Assert.True(await users.IsLockedOutAsync(user));
    }

    [Fact]
    public async Task Email_is_unique_within_a_school_but_not_across_schools()
    {
        using var store = new SqliteIdentityStore();
        using var scope = store.Services.CreateScope();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<AsmsIdentityUser>>();
        var context = scope.ServiceProvider.GetRequiredService<AsmsIdentityDbContext>();
        var schoolA = Guid.NewGuid();
        var email = $"{Guid.NewGuid():N}@example.test";

        Assert.True((await users.CreateAsync(NewUser(schoolA, email), CreateValidPassword())).Succeeded);
        await Assert.ThrowsAsync<DbUpdateException>(
            () => users.CreateAsync(NewUser(schoolA, email), CreateValidPassword()));

        context.ChangeTracker.Clear();
        Assert.True((await users.CreateAsync(
            NewUser(Guid.NewGuid(), email),
            CreateValidPassword())).Succeeded);
    }

    [Fact]
    public async Task Duplicate_user_name_is_rejected()
    {
        using var store = new SqliteIdentityStore();
        using var scope = store.Services.CreateScope();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<AsmsIdentityUser>>();
        var userName = $"user-{Guid.NewGuid():N}";

        Assert.True((await users.CreateAsync(
            NewUser(userName: userName),
            CreateValidPassword())).Succeeded);

        var duplicate = await users.CreateAsync(
            NewUser(userName: userName),
            CreateValidPassword());
        Assert.False(duplicate.Succeeded);
        Assert.Contains(duplicate.Errors, error => error.Code == "DuplicateUserName");
    }

    [Fact]
    public async Task Identity_migration_creates_tables_and_has_no_pending_changes()
    {
        using var store = new SqliteIdentityStore();
        using var scope = store.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AsmsIdentityDbContext>();

        var applied = context.Database.GetAppliedMigrations().ToArray();
        Assert.Single(applied);
        Assert.EndsWith("IdentityInitial", applied[0], StringComparison.Ordinal);
        Assert.False(context.Database.HasPendingModelChanges());

        var tables = await GetTablesAsync(context);
        Assert.Contains("Identity_Users", tables);
        Assert.Contains("Identity_UserClaims", tables);
        Assert.Contains("Identity_UserLogins", tables);
        Assert.Contains("Identity_UserTokens", tables);
    }

    [Fact]
    public async Task Main_and_identity_contexts_migrate_into_the_same_database()
    {
        using var store = new SqliteIdentityStore();
        var mainOptions = new DbContextOptionsBuilder<Asms.Infrastructure.Persistence.AsmsDbContext>()
            .UseSqlite(
                store.Connection,
                sqlite => sqlite.MigrationsAssembly("Asms.Infrastructure.Migrations.Sqlite"))
            .Options;
        using var mainContext = new Asms.Infrastructure.Persistence.AsmsDbContext(
            mainOptions,
            new Asms.Infrastructure.Tests.Persistence.TestTenant());
        using var scope = store.Services.CreateScope();
        var identityContext = scope.ServiceProvider.GetRequiredService<AsmsIdentityDbContext>();

        await mainContext.Database.MigrateAsync();
        await identityContext.Database.MigrateAsync();

        var tables = await GetTablesAsync(identityContext);
        Assert.Contains("Schools", tables);
        Assert.Contains("Identity_Users", tables);
        Assert.Contains("Identity_UserClaims", tables);
        Assert.Contains("Identity_UserLogins", tables);
        Assert.Contains("Identity_UserTokens", tables);
    }

    private static async Task<HashSet<string>> GetTablesAsync(AsmsIdentityDbContext context)
    {
        var tables = new HashSet<string>(StringComparer.Ordinal);
        await using var command = context.Database.GetDbConnection().CreateCommand();
        command.CommandText = "SELECT name FROM sqlite_master WHERE type = 'table'";
        await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            tables.Add(reader.GetString(0));
        }

        return tables;
    }

    private static AsmsIdentityUser NewUser(Guid? schoolId = null, string? email = null, string? userName = null)
    {
        var name = userName ?? $"user-{Guid.NewGuid():N}";
        return new AsmsIdentityUser
        {
            Id = Guid.NewGuid(),
            UserName = name,
            Email = email ?? $"{Guid.NewGuid():N}@example.test",
            SchoolId = schoolId ?? Guid.NewGuid()
        };
    }

    private static string CreateValidPassword() => $"Aa1{Guid.NewGuid():N}";

    private sealed class SqliteIdentityStore : IDisposable
    {
        public SqliteIdentityStore()
        {
            Connection = new SqliteConnection("Data Source=:memory:");
            Connection.Open();

            var services = new ServiceCollection();
            services.AddLogging();
            services.AddAsmsIdentity(options =>
                options.UseSqlite(
                    Connection,
                    sqlite => sqlite.MigrationsAssembly("Asms.Infrastructure.Migrations.Sqlite")));
            Services = services.BuildServiceProvider();

            using var scope = Services.CreateScope();
            scope.ServiceProvider.GetRequiredService<AsmsIdentityDbContext>().Database.Migrate();
        }

        public SqliteConnection Connection { get; }

        public ServiceProvider Services { get; }

        public void Dispose()
        {
            Services.Dispose();
            Connection.Dispose();
        }
    }
}
