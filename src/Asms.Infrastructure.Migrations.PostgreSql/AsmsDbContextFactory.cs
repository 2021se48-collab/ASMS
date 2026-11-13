using Asms.Application.Abstractions;
using Asms.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Asms.Infrastructure.Migrations.PostgreSql;

/// <summary>
/// Used only by the dotnet-ef tool, never at runtime.
/// </summary>
public sealed class AsmsDbContextFactory : IDesignTimeDbContextFactory<AsmsDbContext>
{
    public AsmsDbContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<AsmsDbContext>()
            .UseNpgsql(
                "Host=localhost;Database=asms_design;Username=design;Password=design",
                npgsql => npgsql.MigrationsAssembly("Asms.Infrastructure.Migrations.PostgreSql"))
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
