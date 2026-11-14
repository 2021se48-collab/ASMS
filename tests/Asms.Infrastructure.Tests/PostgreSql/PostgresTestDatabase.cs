using Asms.Infrastructure.Persistence;
using Asms.Infrastructure.Identity;
using Asms.Infrastructure.Tests.Persistence;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace Asms.Infrastructure.Tests.PostgreSql;

public sealed class PostgresTestDatabase : IAsyncDisposable
{
    private readonly string _adminConnectionString;
    private readonly string _databaseConnectionString;

    private PostgresTestDatabase(string adminConnectionString, string databaseName)
    {
        _adminConnectionString = adminConnectionString;
        DatabaseName = databaseName;
        var connectionStringBuilder = new NpgsqlConnectionStringBuilder(adminConnectionString)
        {
            Database = databaseName,
            Pooling = false
        };
        _databaseConnectionString = connectionStringBuilder.ConnectionString;
    }

    public string DatabaseName { get; }

    public static async Task<PostgresTestDatabase> CreateAsync()
    {
        var adminConnectionString = Environment.GetEnvironmentVariable("ASMS_TEST_POSTGRES");
        if (string.IsNullOrWhiteSpace(adminConnectionString))
        {
            throw new InvalidOperationException("ASMS_TEST_POSTGRES is not set");
        }

        var databaseName = $"asms_test_{Guid.NewGuid():N}".ToLowerInvariant();
        await using var adminConnection = new NpgsqlConnection(adminConnectionString);
        await adminConnection.OpenAsync();
        await using (var command = adminConnection.CreateCommand())
        {
            command.CommandText = $"CREATE DATABASE \"{databaseName}\"";
            await command.ExecuteNonQueryAsync();
        }

        return new PostgresTestDatabase(adminConnectionString, databaseName);
    }

    public AsmsDbContext CreateContext(TestTenant tenant)
    {
        var options = new DbContextOptionsBuilder<AsmsDbContext>()
            .UseNpgsql(
                _databaseConnectionString,
                npgsql => npgsql.MigrationsAssembly("Asms.Infrastructure.Migrations.PostgreSql"))
            .Options;
        return new AsmsDbContext(options, tenant);
    }

    public void ConfigureIdentityDbContext(DbContextOptionsBuilder options)
    {
        options.UseNpgsql(
            _databaseConnectionString,
            npgsql => npgsql.MigrationsAssembly("Asms.Infrastructure.Migrations.PostgreSql"));
    }

    public async ValueTask DisposeAsync()
    {
        NpgsqlConnection.ClearAllPools();
        try
        {
            await using var adminConnection = new NpgsqlConnection(_adminConnectionString);
            await adminConnection.OpenAsync();
            await using var command = adminConnection.CreateCommand();
            command.CommandText = $"DROP DATABASE IF EXISTS \"{DatabaseName}\" WITH (FORCE)";
            await command.ExecuteNonQueryAsync();
        }
        finally
        {
            NpgsqlConnection.ClearAllPools();
        }
    }
}
