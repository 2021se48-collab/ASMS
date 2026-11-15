using System.Data.Common;
using System.Text.RegularExpressions;
using Asms.Application.Abstractions;
using Asms.Domain.Schools;
using Asms.Domain.Security;
using Asms.Infrastructure.Identity;
using Asms.Infrastructure.Persistence;
using Asms.Infrastructure.Persistence.Seeding;
using Microsoft.AspNetCore.Identity;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Npgsql;

namespace Asms.Setup;

public sealed class SetupRequest
{
    public string Provider { get; init; } = string.Empty;
    public string ConnectionString { get; init; } = string.Empty;
    public string SchoolName { get; init; } = string.Empty;
    public string SchoolCode { get; init; } = string.Empty;
    public string? Province { get; init; }
    public string? District { get; init; }
    public string DefaultLanguage { get; init; } = "en";
    public string OwnerFullName { get; init; } = string.Empty;
    public string OwnerUserName { get; init; } = string.Empty;
    public string? OwnerEmail { get; init; }
    public string? OwnerPhone { get; init; }
    public string OwnerPassword { get; init; } = string.Empty;

    public override string ToString() => nameof(SetupRequest);
}

public sealed record SetupResult(bool Succeeded, string? ErrorCode, Guid? SchoolId);

public sealed class SetupService
{
    private static readonly Regex SchoolCodePattern = new(
        "^[A-Za-z0-9-]{2,20}$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);

    public async Task<SetupResult> RunAsync(
        SetupRequest request,
        CancellationToken cancellationToken = default)
    {
        if (!IsValid(request))
        {
            return Failure("SETUP_INVALID_INPUT");
        }

        var values = Normalize(request);
        var tenant = new SetupTenant();

        try
        {
            await using var connection = CreateConnection(values.Provider, values.ConnectionString);
            await connection.OpenAsync(cancellationToken);

            var migrationsAssembly = GetMigrationsAssembly(values.Provider);
            var applicationOptions = CreateApplicationOptions(
                connection,
                values.Provider,
                migrationsAssembly);
            await using var applicationDb = new AsmsDbContext(applicationOptions, tenant);

            var identityServices = new ServiceCollection();
            identityServices.AddLogging();
            identityServices.AddAsmsIdentity(options =>
                ConfigureIdentityDatabase(options, connection, values.Provider, migrationsAssembly));
            using var identityProvider = identityServices.BuildServiceProvider();
            await using var scope = identityProvider.CreateAsyncScope();
            var identityDb = scope.ServiceProvider.GetRequiredService<AsmsIdentityDbContext>();
            var userManager = scope.ServiceProvider.GetRequiredService<UserManager<AsmsIdentityUser>>();

            await applicationDb.Database.MigrateAsync(cancellationToken);
            await identityDb.Database.MigrateAsync(cancellationToken);

            if (await applicationDb.Schools
                    .IgnoreQueryFilters()
                    .AnyAsync(school => school.Code == values.SchoolCode, cancellationToken))
            {
                return Failure("SETUP_SCHOOL_EXISTS");
            }

            await using var transaction =
                await applicationDb.Database.BeginTransactionAsync(cancellationToken);
            await identityDb.Database.UseTransactionAsync(
                transaction.GetDbTransaction(),
                cancellationToken);

            var school = new School
            {
                Name = values.SchoolName,
                Code = values.SchoolCode,
                Province = values.Province,
                District = values.District,
                DefaultLanguage = values.DefaultLanguage,
                CalendarType = "SolarHijri",
                IsActive = true
            };
            applicationDb.Schools.Add(school);
            await applicationDb.SaveChangesAsync(cancellationToken);

            tenant.SchoolId = school.Id;
            await new SchoolRoleSeeder(applicationDb, tenant)
                .SeedAsync(school.Id, cancellationToken);

            var ownerRole = await applicationDb.AppRoles
                .SingleAsync(role => role.Name == SystemRoles.SchoolOwner, cancellationToken);
            var appUser = new AppUser
            {
                SchoolId = school.Id,
                FullName = values.OwnerFullName,
                Email = values.OwnerEmail,
                PhoneNumber = values.OwnerPhone,
                PreferredLanguage = values.DefaultLanguage,
                IsActive = true
            };
            applicationDb.AppUsers.Add(appUser);
            applicationDb.UserRoles.Add(new UserRole
            {
                SchoolId = school.Id,
                UserId = appUser.Id,
                RoleId = ownerRole.Id
            });
            await applicationDb.SaveChangesAsync(cancellationToken);

            var identityUser = new AsmsIdentityUser
            {
                UserName = values.OwnerUserName,
                Email = values.OwnerEmail,
                PhoneNumber = values.OwnerPhone,
                SchoolId = school.Id,
                AppUserId = appUser.Id,
                IsActive = true
            };
            var createResult = await userManager.CreateAsync(identityUser, request.OwnerPassword);
            if (!createResult.Succeeded)
            {
                await transaction.RollbackAsync(cancellationToken);
                var errorCode = createResult.Errors.Any(error =>
                    error.Code.StartsWith("Password", StringComparison.Ordinal))
                    ? "SETUP_WEAK_PASSWORD"
                    : "SETUP_IDENTITY_USER_FAILED";
                return Failure(errorCode);
            }

            appUser.IdentityUserId = identityUser.Id;
            await applicationDb.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);

            return new SetupResult(true, null, school.Id);
        }
        catch (DbUpdateException)
        {
            return Failure("SETUP_DATABASE_ERROR");
        }
        catch (DbException)
        {
            return Failure("SETUP_DATABASE_ERROR");
        }
        catch (InvalidOperationException)
        {
            return Failure("SETUP_DATABASE_ERROR");
        }
        catch (ArgumentException)
        {
            return Failure("SETUP_INVALID_INPUT");
        }
    }

    private static bool IsValid(SetupRequest? request)
    {
        if (request is null ||
            (request.Provider != "Sqlite" && request.Provider != "PostgreSql") ||
            string.IsNullOrWhiteSpace(request.ConnectionString) ||
            string.IsNullOrWhiteSpace(request.SchoolName) ||
            request.SchoolName.Trim().Length > 200 ||
            !SchoolCodePattern.IsMatch(request.SchoolCode?.Trim() ?? string.Empty) ||
            (request.Province?.Trim().Length ?? 0) > 200 ||
            (request.District?.Trim().Length ?? 0) > 200 ||
            request.DefaultLanguage is not ("en" or "ps" or "fa-AF") ||
            string.IsNullOrWhiteSpace(request.OwnerFullName) ||
            request.OwnerFullName.Trim().Length > 200 ||
            string.IsNullOrWhiteSpace(request.OwnerUserName) ||
            request.OwnerUserName.Trim().Length > 256 ||
            (request.OwnerEmail?.Trim().Length ?? 0) > 254 ||
            (request.OwnerPhone?.Trim().Length ?? 0) > 30 ||
            string.IsNullOrWhiteSpace(request.OwnerPassword))
        {
            return false;
        }

        var email = request.OwnerEmail?.Trim();
        return string.IsNullOrEmpty(email) ||
            new System.ComponentModel.DataAnnotations.EmailAddressAttribute().IsValid(email);
    }

    private static SetupValues Normalize(SetupRequest request) =>
        new(
            request.Provider,
            request.ConnectionString,
            request.SchoolName.Trim(),
            request.SchoolCode.Trim(),
            OptionalValue(request.Province),
            OptionalValue(request.District),
            request.DefaultLanguage,
            request.OwnerFullName.Trim(),
            request.OwnerUserName.Trim(),
            OptionalValue(request.OwnerEmail),
            OptionalValue(request.OwnerPhone));

    private static string? OptionalValue(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static DbConnection CreateConnection(string provider, string connectionString) =>
        provider switch
        {
            "Sqlite" => new SqliteConnection(connectionString),
            "PostgreSql" => new NpgsqlConnection(connectionString),
            _ => throw new ArgumentException("The database provider is invalid.", nameof(provider))
        };

    private static string GetMigrationsAssembly(string provider) =>
        provider switch
        {
            "Sqlite" => "Asms.Infrastructure.Migrations.Sqlite",
            "PostgreSql" => "Asms.Infrastructure.Migrations.PostgreSql",
            _ => throw new ArgumentException("The database provider is invalid.", nameof(provider))
        };

    private static DbContextOptions<AsmsDbContext> CreateApplicationOptions(
        DbConnection connection,
        string provider,
        string migrationsAssembly)
    {
        var builder = new DbContextOptionsBuilder<AsmsDbContext>();
        ConfigureDatabase(builder, connection, provider, migrationsAssembly);
        return builder.Options;
    }

    private static void ConfigureIdentityDatabase(
        DbContextOptionsBuilder options,
        DbConnection connection,
        string provider,
        string migrationsAssembly)
    {
        ConfigureDatabase(options, connection, provider, migrationsAssembly);
    }

    private static void ConfigureDatabase(
        DbContextOptionsBuilder options,
        DbConnection connection,
        string provider,
        string migrationsAssembly)
    {
        if (provider == "Sqlite")
        {
            options.UseSqlite(connection, sqlite => sqlite.MigrationsAssembly(migrationsAssembly));
            return;
        }

        options.UseNpgsql(connection, postgres => postgres.MigrationsAssembly(migrationsAssembly));
    }

    private static SetupResult Failure(string errorCode) =>
        new(false, errorCode, null);

    private sealed class SetupTenant : ICurrentTenant
    {
        public Guid? SchoolId { get; set; }
        public Guid? UserId => null;
        public Guid? DeviceId => null;
    }

    private sealed record SetupValues(
        string Provider,
        string ConnectionString,
        string SchoolName,
        string SchoolCode,
        string? Province,
        string? District,
        string DefaultLanguage,
        string OwnerFullName,
        string OwnerUserName,
        string? OwnerEmail,
        string? OwnerPhone);
}
