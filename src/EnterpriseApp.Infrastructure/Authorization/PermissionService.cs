using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using EnterpriseApp.Application.Common.Authorization;
using EnterpriseApp.Application.Common.Interfaces;
using EnterpriseApp.Domain.Authorization;
using EnterpriseApp.Domain.Entities.Authorization;
using EnterpriseApp.Domain.Interfaces.Repositories.Authorization;
using EnterpriseApp.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;
using Microsoft.Extensions.Logging;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;

namespace EnterpriseApp.Infrastructure.Authorization;

/// <summary>
/// Resolves the effective permission set for a user, combining direct role assignments
/// and parent-role inheritance. Caches results in <see cref="ICacheService"/> for 15 minutes;
/// invalidated by <c>UserPermissionsChanged</c> domain events.
/// </summary>
public sealed class PermissionService(
    AppDbContext               db,
    ICacheService              cache,
    IConfiguration             config,
    ILogger<PermissionService> logger)
    : IPermissionService
{
    private static readonly TimeSpan CacheTtl = TimeSpan.FromMinutes(15);

    public async Task<IReadOnlySet<string>> GetPermissionsForUserAsync(
        string userId, Guid? tenantId, CancellationToken ct = default)
    {
        var key    = AuthorizationCacheKeys.UserPermissions(userId, tenantId);
        var cached = await cache.GetAsync<CachedPermissions>(key, ct);
        if (cached is not null) return cached.Permissions.ToHashSet(StringComparer.Ordinal);

        var resolved = await ResolveFromDbAsync(userId, tenantId, ct);

        await cache.SetAsync(key, new CachedPermissions([..resolved]), CacheTtl, ct);
        logger.LogDebug("Resolved {Count} permissions for user {UserId} (tenant {TenantId})",
            resolved.Count, userId, tenantId);
        return resolved;
    }

    public async Task<bool> HasPermissionAsync(
        string userId, Guid? tenantId, string code, CancellationToken ct = default)
    {
        var perms = await GetPermissionsForUserAsync(userId, tenantId, ct);
        return perms.Contains(PermissionCodes.Wildcard) || perms.Contains(code);
    }

    public string SignRoleSnapshot(IEnumerable<string> permissionCodes, string roleName)
    {
        var key = config["Jwt:Key"]
            ?? throw new InvalidOperationException("Jwt:Key is not configured.");
        var sortedCodes = permissionCodes.OrderBy(c => c, StringComparer.Ordinal).ToArray();

        var creds = new SigningCredentials(
            new SymmetricSecurityKey(Encoding.UTF8.GetBytes(key)),
            SecurityAlgorithms.HmacSha256);

        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, roleName),
            new("kind", "role-snapshot"),
            new("psv", ComputeSnapshotVersion(sortedCodes)),
            new("perms", JsonSerializer.Serialize(sortedCodes), JsonClaimValueTypes.JsonArray),
        };

        var token = new JwtSecurityToken(
            issuer:             config["Jwt:Issuer"],
            audience:           config["Jwt:Audience"],
            claims:             claims,
            notBefore:          DateTime.UtcNow,
            expires:            DateTime.UtcNow.AddDays(30),
            signingCredentials: creds);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    public string ComputeSnapshotVersion(IEnumerable<string> permissionCodes)
    {
        var sorted = permissionCodes.OrderBy(c => c, StringComparer.Ordinal);
        var bytes  = Encoding.UTF8.GetBytes(string.Join('|', sorted));
        var hash   = SHA256.HashData(bytes);
        // Short, URL-safe identifier (12 bytes hex = 24 chars) — enough for non-cryptographic versioning.
        return Convert.ToHexString(hash, 0, 12).ToLowerInvariant();
    }

    // ── Internal ─────────────────────────────────────────────────────────────

    private async Task<HashSet<string>> ResolveFromDbAsync(
        string userId, Guid? tenantId, CancellationToken ct)
    {
        // 1. Roles directly assigned to the user
        var directRoleIds = await db.UserRoles
            .AsNoTracking()
            .Where(ur => ur.UserId == userId)
            .Select(ur => ur.RoleId)
            .ToListAsync(ct);

        if (directRoleIds.Count == 0)
            return [];

        // 2. Inherited roles via ParentRoleId chain (BFS)
        var allRoleIds = new HashSet<RoleId>(directRoleIds);
        var frontier   = new Queue<RoleId>(directRoleIds);
        while (frontier.Count > 0)
        {
            var current = frontier.Dequeue();
            var parent  = await db.Roles
                .AsNoTracking()
                .Where(r => r.Id == current)
                .Select(r => r.ParentRoleId)
                .FirstOrDefaultAsync(ct);
            if (parent is not null && allRoleIds.Add(parent))
                frontier.Enqueue(parent);
        }

        // 3. Permissions via RolePermission join, dropping expired ones
        var now = DateTimeOffset.UtcNow;
        var permissionIds = await db.RolePermissions
            .AsNoTracking()
            .Where(rp => allRoleIds.Contains(rp.RoleId))
            .Where(rp => rp.ExpiresAt == null || rp.ExpiresAt > now)
            .Select(rp => rp.PermissionId)
            .Distinct()
            .ToListAsync(ct);

        var codes = await db.Permissions
            .AsNoTracking()
            .Where(p => permissionIds.Contains(p.Id))
            .Where(p => p.TenantId == null || p.TenantId == tenantId)
            .Select(p => EF.Property<string>(p, "Code"))
            .ToListAsync(ct);

        return [..codes];
    }

    private sealed class CachedPermissions(IReadOnlyList<string> permissions)
    {
        public IReadOnlyList<string> Permissions { get; init; } = permissions;
    }
}
