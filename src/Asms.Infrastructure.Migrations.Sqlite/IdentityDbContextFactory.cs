using Asms.Infrastructure.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Asms.Infrastructure.Migrations.Sqlite;

public sealed class IdentityDbContextFactory : IDesignTimeDbContextFactory<AsmsIdentityDbContext>
{
    public AsmsIdentityDbContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<AsmsIdentityDbContext>()
            .UseSqlite(
                "Data Source=asms-identity-design.db",
                sqlite => sqlite.MigrationsAssembly("Asms.Infrastructure.Migrations.Sqlite"))
            .Options;

        return new AsmsIdentityDbContext(options);
    }
}
