using Asms.Domain.Schools;
using Asms.Domain.Security;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Asms.Infrastructure.Persistence.Configurations;

public sealed class AppUserConfiguration : IEntityTypeConfiguration<AppUser>
{
    public void Configure(EntityTypeBuilder<AppUser> builder)
    {
        builder.Property(user => user.FullName).IsRequired().HasMaxLength(200);
        builder.Property(user => user.Email).HasMaxLength(254);
        builder.Property(user => user.PhoneNumber).HasMaxLength(30);
        builder.Property(user => user.PreferredLanguage).IsRequired().HasMaxLength(10);

        builder.HasIndex(user => user.SchoolId)
            .HasDatabaseName("IX_AppUsers_SchoolId");
        builder.HasIndex(user => new { user.SchoolId, user.Email })
            .HasDatabaseName("IX_AppUsers_SchoolId_Email");
        builder.HasIndex(user => new { user.SchoolId, user.PhoneNumber })
            .HasDatabaseName("IX_AppUsers_SchoolId_PhoneNumber");
        builder.HasIndex(user => user.IdentityUserId)
            .HasDatabaseName("IX_AppUsers_IdentityUserId");
    }
}

public sealed class AppRoleConfiguration : IEntityTypeConfiguration<AppRole>
{
    public void Configure(EntityTypeBuilder<AppRole> builder)
    {
        builder.Property(role => role.Name).IsRequired().HasMaxLength(200);
        builder.Property(role => role.Description).HasMaxLength(2000);

        builder.HasIndex(role => role.SchoolId)
            .HasDatabaseName("IX_AppRoles_SchoolId");
        builder.HasIndex(role => new { role.SchoolId, role.Name })
            .IsUnique()
            .HasDatabaseName("UX_AppRoles_SchoolId_Name");
        builder.HasOne<School>()
            .WithMany()
            .HasForeignKey(role => role.SchoolId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class UserRoleConfiguration : IEntityTypeConfiguration<UserRole>
{
    public void Configure(EntityTypeBuilder<UserRole> builder)
    {
        builder.HasIndex(userRole => userRole.SchoolId)
            .HasDatabaseName("IX_UserRoles_SchoolId");
        builder.HasIndex(userRole => new { userRole.SchoolId, userRole.UserId, userRole.RoleId })
            .IsUnique()
            .HasDatabaseName("UX_UserRoles_SchoolId_UserId_RoleId");
        builder.HasOne<AppUser>()
            .WithMany()
            .HasForeignKey(userRole => userRole.UserId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<AppRole>()
            .WithMany()
            .HasForeignKey(userRole => userRole.RoleId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class RolePermissionConfiguration : IEntityTypeConfiguration<RolePermission>
{
    public void Configure(EntityTypeBuilder<RolePermission> builder)
    {
        builder.Property(permission => permission.Permission).IsRequired().HasMaxLength(200);

        builder.HasIndex(permission => permission.SchoolId)
            .HasDatabaseName("IX_RolePermissions_SchoolId");
        builder.HasIndex(permission => new
            {
                permission.SchoolId,
                permission.RoleId,
                permission.Permission
            })
            .IsUnique()
            .HasDatabaseName("UX_RolePermissions_SchoolId_RoleId_Permission");
        builder.HasOne<School>()
            .WithMany()
            .HasForeignKey(permission => permission.SchoolId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<AppRole>()
            .WithMany()
            .HasForeignKey(permission => permission.RoleId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
