using Asms.Infrastructure.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Asms.Infrastructure.Migrations.PostgreSql;

public sealed class IdentityDbContextFactory : IDesignTimeDbContextFactory<AsmsIdentityDbContext>
{
    public AsmsIdentityDbContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<AsmsIdentityDbContext>()
            .UseNpgsql(
                "Host=localhost;Database=asms_identity_design;Username=design",
                npgsql => npgsql.MigrationsAssembly("Asms.Infrastructure.Migrations.PostgreSql"))
            .Options;

        return new AsmsIdentityDbContext(options);
    }
}
