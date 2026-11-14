using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace Asms.Infrastructure.Identity;

public class AsmsIdentityDbContext : IdentityUserContext<AsmsIdentityUser, Guid>
{
    public AsmsIdentityDbContext(DbContextOptions<AsmsIdentityDbContext> options)
        : base(options)
    {
    }

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        builder.Entity<AsmsIdentityUser>(user =>
        {
            user.ToTable("Identity_Users");
            user.Property(item => item.SchoolId).IsRequired();
            user.Property(item => item.UserName).HasMaxLength(256);
            user.Property(item => item.Email).HasMaxLength(254);
            user.HasIndex(item => item.SchoolId)
                .HasDatabaseName("IX_Identity_Users_SchoolId");
            user.HasIndex(item => new { item.SchoolId, item.NormalizedEmail })
                .IsUnique()
                .HasDatabaseName("UX_Identity_Users_SchoolId_NormalizedEmail");
            user.HasIndex(item => item.AppUserId)
                .HasDatabaseName("IX_Identity_Users_AppUserId");
        });

        builder.Entity<IdentityUserClaim<Guid>>().ToTable("Identity_UserClaims");
        builder.Entity<IdentityUserLogin<Guid>>().ToTable("Identity_UserLogins");
        builder.Entity<IdentityUserToken<Guid>>().ToTable("Identity_UserTokens");

        // This context has no tenant filter so platform accounts can be managed.
    }
}
