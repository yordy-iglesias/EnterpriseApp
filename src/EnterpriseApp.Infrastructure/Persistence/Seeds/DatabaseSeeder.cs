using EnterpriseApp.Domain.Entities.Authorization;
using EnterpriseApp.Domain.ValueObjects;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace EnterpriseApp.Infrastructure.Persistence.Seeds;

/// <summary>
/// Idempotent seeder for authorization reference data. Run on startup or via the
/// migration service. Safe to call multiple times — it inserts only what is missing.
/// </summary>
public sealed class DatabaseSeeder(AppDbContext db, ILogger<DatabaseSeeder> logger)
{
    public async Task SeedAsync(CancellationToken ct = default)
    {
        await SeedPermissionsAsync(ct);
        await SeedRolesAsync(ct);
        await SeedRolePermissionsAsync(ct);
        await db.SaveChangesAsync(ct);
        logger.LogInformation("Authorization seed completed.");
    }

    private async Task SeedPermissionsAsync(CancellationToken ct)
    {
        var existingCodes = await db.Permissions
                                    .IgnoreQueryFilters()
                                    .Select(p => (string)p.Code)
                                    .ToListAsync(ct);

        foreach (var item in PermissionsSeed.Items)
        {
            if (existingCodes.Contains(item.Code, StringComparer.Ordinal)) continue;

            var permission = Permission.Create(
                code:        item.Code,
                displayName: item.DisplayName,
                description: item.Description,
                isSensitive: item.IsSensitive,
                isSystem:    true,
                tenantId:    null,
                createdBy:   "system-seed");

            await db.Permissions.AddAsync(permission, ct);
            logger.LogDebug("Seeded permission {Code}", item.Code);
        }
    }

    private async Task SeedRolesAsync(CancellationToken ct)
    {
        var existingNames = await db.Roles
                                    .IgnoreQueryFilters()
                                    .Where(r => r.TenantId == null)
                                    .Select(r => r.NormalizedName)
                                    .ToListAsync(ct);

        foreach (var seed in PermissionsSeed.SystemRoles)
        {
            var normalized = seed.Name.ToUpperInvariant();
            if (existingNames.Contains(normalized, StringComparer.Ordinal)) continue;

            var role = Role.Create(
                name:           seed.Name,
                normalizedName: normalized,
                description:    seed.Description,
                tenantId:       null,
                parentRoleId:   null,
                isSystem:       true,
                createdBy:      "system-seed");

            await db.Roles.AddAsync(role, ct);
            logger.LogDebug("Seeded role {Name}", seed.Name);
        }
    }

    private async Task SeedRolePermissionsAsync(CancellationToken ct)
    {
        // Save first so role/permission Ids are available for the join inserts.
        await db.SaveChangesAsync(ct);

        var allRoles = await db.Roles
                               .IgnoreQueryFilters()
                               .Where(r => r.TenantId == null)
                               .Include(r => r.RolePermissions)
                               .ToListAsync(ct);

        var allPermissions = await db.Permissions
                                     .IgnoreQueryFilters()
                                     .Where(p => p.TenantId == null)
                                     .ToListAsync(ct);

        foreach (var seedItem in PermissionsSeed.Items)
        {
            foreach (var roleName in seedItem.DefaultRoles)
            {
                var role = allRoles.FirstOrDefault(
                    r => r.NormalizedName == roleName.ToUpperInvariant());
                if (role is null) continue;

                var permission = allPermissions.FirstOrDefault(
                    p => (string)p.Code == seedItem.Code);
                if (permission is null) continue;

                if (role.RolePermissions.Any(rp => rp.PermissionId == permission.Id))
                    continue;

                role.GrantPermission(permission, "system-seed");
            }
        }
    }
}
