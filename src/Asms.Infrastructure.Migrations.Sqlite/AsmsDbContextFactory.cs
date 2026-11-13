using Asms.Application.Abstractions;
using Asms.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Asms.Infrastructure.Migrations.Sqlite;

/// <summary>
/// Used only by the dotnet-ef tool, never at runtime.
/// </summary>
public sealed class AsmsDbContextFactory : IDesignTimeDbContextFactory<AsmsDbContext>
{
    public AsmsDbContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<AsmsDbContext>()
            .UseSqlite(
                "Data Source=asms-design.db",
                sqlite => sqlite.MigrationsAssembly("Asms.Infrastructure.Migrations.Sqlite"))
            .Options;

        return new AsmsDbContext(options, new DesignTimeTenant());
    }

    private sealed class DesignTimeTenant : ICurrentTenant
    {
        public Guid? SchoolId => null;

        public Guid? UserId => null;

        public Guid? DeviceId => null;
    }
}
