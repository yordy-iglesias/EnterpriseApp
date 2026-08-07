using EnterpriseApp.Domain.Entities.Authorization;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace EnterpriseApp.Infrastructure.Persistence.Configurations.Authorization;

public sealed class UserRoleConfiguration : IEntityTypeConfiguration<UserRole>
{
    public void Configure(EntityTypeBuilder<UserRole> builder)
    {
        builder.ToTable("UserRoles", "auth");

        builder.HasKey(ur => ur.Id);
        builder.Property(ur => ur.Id).ValueGeneratedNever();

        builder.Property(ur => ur.UserId).IsRequired().HasMaxLength(450);
        builder.Property(ur => ur.RoleId)
               .HasConversion(id => id.Value, value => RoleId.From(value))
               .IsRequired();
        builder.Property(ur => ur.TenantId).IsRequired(false);
        builder.Property(ur => ur.AssignedBy).IsRequired().HasMaxLength(256);
        builder.Property(ur => ur.AssignedAt).IsRequired();

        builder.HasOne<Role>()
               .WithMany()
               .HasForeignKey(ur => ur.RoleId)
               .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(ur => new { ur.UserId, ur.RoleId }).IsUnique();
        builder.HasIndex(ur => ur.UserId);
        builder.HasIndex(ur => ur.RoleId);
        builder.HasIndex(ur => ur.TenantId);
    }
}
