using EnterpriseApp.Domain.Entities.Authorization;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace EnterpriseApp.Infrastructure.Persistence.Configurations.Authorization;

/// <summary>
/// EF Core mapping for the append-only <see cref="PermissionAuditLog"/>.
/// <para>The migration must add a DB-level trigger preventing UPDATE/DELETE on this table.
/// See <c>.claude/rules/audit-logging.md §3</c>.</para>
/// </summary>
public sealed class PermissionAuditLogConfiguration : IEntityTypeConfiguration<PermissionAuditLog>
{
    public void Configure(EntityTypeBuilder<PermissionAuditLog> builder)
    {
        // Hint to EF that the table has a trigger so it doesn't try to use OUTPUT clause.
        builder.ToTable("PermissionAuditLog", "auth", t =>
        {
            t.HasTrigger("tr_PermissionAuditLog_NoUpdateDelete");
        });

        builder.HasKey(l => l.Id);
        builder.Property(l => l.Id).ValueGeneratedNever();

        builder.Property(l => l.PerformedAt).IsRequired();
        builder.Property(l => l.Action).HasConversion<string>().HasMaxLength(48).IsRequired();

        builder.Property(l => l.PerformedBy).IsRequired().HasMaxLength(256);
        builder.Property(l => l.PerformedByEmail).HasMaxLength(320);

        builder.Property(l => l.TargetUserId).HasMaxLength(450);
        builder.Property(l => l.TargetRoleId)
               .HasConversion(
                   id    => id != null ? (Guid?)id.Value : null,
                   value => value.HasValue ? RoleId.From(value.Value) : null);
        builder.Property(l => l.TargetPermissionId)
               .HasConversion(
                   id    => id != null ? (Guid?)id.Value : null,
                   value => value.HasValue ? PermissionId.From(value.Value) : null);

        builder.Property(l => l.TenantId).IsRequired(false);
        builder.Property(l => l.IpAddress).HasMaxLength(64);
        builder.Property(l => l.UserAgent).HasMaxLength(512);
        builder.Property(l => l.CorrelationId).HasMaxLength(64);
        builder.Property(l => l.Reason).HasMaxLength(1024);
        builder.Property(l => l.MetadataJson).HasColumnType("nvarchar(max)");

        builder.HasIndex(l => l.PerformedAt);
        builder.HasIndex(l => new { l.TenantId, l.PerformedAt });
        builder.HasIndex(l => new { l.TargetUserId, l.PerformedAt });
        builder.HasIndex(l => new { l.PerformedBy, l.PerformedAt });
        builder.HasIndex(l => l.Action);
    }
}
