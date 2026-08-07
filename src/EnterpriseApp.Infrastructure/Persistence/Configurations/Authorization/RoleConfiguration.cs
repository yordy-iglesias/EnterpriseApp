using EnterpriseApp.Domain.Entities.Authorization;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace EnterpriseApp.Infrastructure.Persistence.Configurations.Authorization;

public sealed class RoleConfiguration : IEntityTypeConfiguration<Role>
{
    public void Configure(EntityTypeBuilder<Role> builder)
    {
        builder.ToTable("Roles", "auth");

        builder.HasKey(r => r.Id);
        builder.Property(r => r.Id)
               .HasConversion(id => id.Value, value => RoleId.From(value))
               .ValueGeneratedNever();

        builder.Property(r => r.Name).IsRequired().HasMaxLength(64);
        builder.Property(r => r.NormalizedName).IsRequired().HasMaxLength(64);
        builder.Property(r => r.Description).HasMaxLength(512);
        builder.Property(r => r.TenantId).IsRequired(false);
        builder.Property(r => r.IsSystem).IsRequired().HasDefaultValue(false);

        // Hierarchy — self reference
        builder.Property(r => r.ParentRoleId)
               .HasConversion(
                   id    => id != null ? (Guid?)id.Value : null,
                   value => value.HasValue ? RoleId.From(value.Value) : null);

        // PermissionsSnapshot — replaces the legacy NHCS abuse of
        // IdentityRole.ConcurrencyStamp. See .claude/rules/authorization.md §4.
        builder.Property(r => r.PermissionsSnapshot)
               .HasMaxLength(8192)
               .IsRequired(false);
        builder.Property(r => r.PermissionsSnapshotUpdatedAt).IsRequired(false);

        // Audit
        builder.Property(r => r.CreatedAt).IsRequired();
        builder.Property(r => r.CreatedBy).HasMaxLength(256);
        builder.Property(r => r.UpdatedAt).IsRequired(false);
        builder.Property(r => r.UpdatedBy).HasMaxLength(256);
        builder.Property(r => r.IsDeleted).IsRequired().HasDefaultValue(false);
        builder.Property(r => r.DeletedAt).IsRequired(false);

        // RolePermissions one-to-many
        builder.HasMany(r => r.RolePermissions)
               .WithOne()
               .HasForeignKey(rp => rp.RoleId)
               .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(r => new { r.NormalizedName, r.TenantId }).IsUnique();
        builder.HasIndex(r => r.TenantId);
        builder.HasIndex(r => r.IsDeleted);

        builder.HasQueryFilter(r => !r.IsDeleted);
    }
}
