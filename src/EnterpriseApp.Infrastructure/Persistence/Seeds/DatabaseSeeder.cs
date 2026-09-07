using EnterpriseApp.Application.Common.Auth;
using EnterpriseApp.Application.Common.Interfaces;
using EnterpriseApp.Domain.Entities.Authorization;
using EnterpriseApp.Domain.Entities.Identity;
using EnterpriseApp.Domain.ValueObjects;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace EnterpriseApp.Infrastructure.Persistence.Seeds;

/// <summary>
/// Idempotent seeder for authorization reference data. Run on startup or via the
/// migration service. Safe to call multiple times — it inserts only what is missing.
/// </summary>
public sealed class DatabaseSeeder(
    AppDbContext              db,
    IPasswordHasher           hasher,
    IOptions<AuthOptions>     authOptions,
    ILogger<DatabaseSeeder>   logger)
{
    private readonly AuthOptions _authOptions = authOptions.Value;

    public async Task SeedAsync(CancellationToken ct = default)
    {
        await SeedPermissionsAsync(ct);
        await SeedRolesAsync(ct);
        await SeedRolePermissionsAsync(ct);
        await db.SaveChangesAsync(ct);
        logger.LogInformation("Authorization seed completed.");

        await SeedAdminUserAsync(ct);
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

    private async Task SeedAdminUserAsync(CancellationToken ct)
    {
        var seedOpts = _authOptions.SeedAdmin;
        if (!seedOpts.Enabled) return;

        var normalized = seedOpts.Email.Trim().ToLowerInvariant();
        var exists = await db.Users.IgnoreQueryFilters()
            .AnyAsync(u => u.NormalizedEmail == normalized, ct);
        if (exists) return;

        var hash = hasher.Hash(seedOpts.Password);
        var user = User.Create(
            email:        Email.From(seedOpts.Email),
            passwordHash: hash,
            firstName:    seedOpts.FirstName,
            lastName:     seedOpts.LastName,
            tenantId:     null,
            createdBy:    "system-seed");

        user.ConfirmEmail();

        var superAdmin = await db.Roles.IgnoreQueryFilters()
            .FirstOrDefaultAsync(r => r.NormalizedName == "SUPER-ADMIN", ct);

        await db.Users.AddAsync(user, ct);
        await db.SaveChangesAsync(ct);

        if (superAdmin is not null)
        {
            var userRole = UserRole.Create(
                userId:     user.Id.ToString(),
                roleId:     superAdmin.Id,
                tenantId:   null,
                assignedBy: "system-seed");
            await db.UserRoles.AddAsync(userRole, ct);
            await db.SaveChangesAsync(ct);
        }

        if (seedOpts.Password == "Admin123!")
            logger.LogWarning("Admin user seeded with DEFAULT password. Change it before deploying to production!");
        else
            logger.LogInformation("Admin user seeded: {Email}", seedOpts.Email);
    }
}
