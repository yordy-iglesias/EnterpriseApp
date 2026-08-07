using EnterpriseApp.Domain.Entities.Authorization;
using EnterpriseApp.Domain.ValueObjects;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace EnterpriseApp.Infrastructure.Persistence.Configurations.Authorization;

public sealed class PermissionConfiguration : IEntityTypeConfiguration<Permission>
{
    public void Configure(EntityTypeBuilder<Permission> builder)
    {
        builder.ToTable("Permissions", "auth");

        builder.HasKey(p => p.Id);
        builder.Property(p => p.Id)
               .HasConversion(id => id.Value, value => PermissionId.From(value))
               .ValueGeneratedNever();

        // PermissionCode is a Value Object → stored as a single string column
        // with a converter. Module/Action/Qualifier are derived properties (ignored).
        builder.Property(p => p.Code)
               .HasConversion(
                   code  => code.Value,
                   value => PermissionCode.From(value))
               .HasColumnName("Code")
               .HasMaxLength(128)
               .IsRequired();

        builder.Property(p => p.DisplayName).IsRequired().HasMaxLength(200);
        builder.Property(p => p.Description).HasMaxLength(1024);
        builder.Property(p => p.IsSensitive).IsRequired().HasDefaultValue(false);
        builder.Property(p => p.IsSystem).IsRequired().HasDefaultValue(false);
        builder.Property(p => p.TenantId).IsRequired(false);

        // Audit columns
        builder.Property(p => p.CreatedAt).IsRequired();
        builder.Property(p => p.CreatedBy).HasMaxLength(256);
        builder.Property(p => p.UpdatedAt).IsRequired(false);
        builder.Property(p => p.UpdatedBy).HasMaxLength(256);
        builder.Property(p => p.IsDeleted).IsRequired().HasDefaultValue(false);
        builder.Property(p => p.DeletedAt).IsRequired(false);

        // Unique per (Code, TenantId) — TenantId NULL = global
        builder.HasIndex(p => new { p.Code, p.TenantId }).IsUnique();
        builder.HasIndex(p => p.IsSensitive);

        builder.HasQueryFilter(p => !p.IsDeleted);
    }
}
