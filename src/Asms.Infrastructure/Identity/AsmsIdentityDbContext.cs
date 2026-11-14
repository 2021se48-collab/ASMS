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

    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();

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

        builder.Entity<RefreshToken>(token =>
        {
            token.ToTable("Identity_RefreshTokens");
            token.HasKey(item => item.Id);
            token.Property(item => item.Id).ValueGeneratedNever();
            token.Property(item => item.TokenHash).IsRequired().HasMaxLength(64);
            token.Property(item => item.SecurityStamp).IsRequired().HasMaxLength(256);
            token.Property(item => item.RevokedReason).HasMaxLength(50);
            token.HasIndex(item => item.TokenHash)
                .IsUnique()
                .HasDatabaseName("UX_Identity_RefreshTokens_TokenHash");
            token.HasIndex(item => item.UserId)
                .HasDatabaseName("IX_Identity_RefreshTokens_UserId");
            token.HasIndex(item => item.FamilyId)
                .HasDatabaseName("IX_Identity_RefreshTokens_FamilyId");
            token.HasIndex(item => item.ExpiresAtUtc)
                .HasDatabaseName("IX_Identity_RefreshTokens_ExpiresAtUtc");
            token.HasOne<AsmsIdentityUser>()
                .WithMany()
                .HasForeignKey(item => item.UserId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        // This context has no tenant filter so platform accounts can be managed.
    }
}
