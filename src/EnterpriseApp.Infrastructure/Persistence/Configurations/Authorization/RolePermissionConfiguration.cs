using EnterpriseApp.Domain.Entities.Authorization;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace EnterpriseApp.Infrastructure.Persistence.Configurations.Authorization;

public sealed class RolePermissionConfiguration : IEntityTypeConfiguration<RolePermission>
{
    public void Configure(EntityTypeBuilder<RolePermission> builder)
    {
        builder.ToTable("RolePermissions", "auth");

        builder.HasKey(rp => rp.Id);
        builder.Property(rp => rp.Id).ValueGeneratedNever();

        builder.Property(rp => rp.RoleId)
               .HasConversion(id => id.Value, value => RoleId.From(value))
               .IsRequired();

        builder.Property(rp => rp.PermissionId)
               .HasConversion(id => id.Value, value => PermissionId.From(value))
               .IsRequired();

        builder.Property(rp => rp.GrantedBy).IsRequired().HasMaxLength(256);
        builder.Property(rp => rp.GrantedAt).IsRequired();
        builder.Property(rp => rp.ExpiresAt).IsRequired(false);

        // FK to Permission with restrict — never cascade-delete a permission used by a role
        builder.HasOne<Permission>()
               .WithMany()
               .HasForeignKey(rp => rp.PermissionId)
               .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(rp => new { rp.RoleId, rp.PermissionId }).IsUnique();
        builder.HasIndex(rp => rp.PermissionId);
        builder.HasIndex(rp => rp.ExpiresAt);
    }
}
