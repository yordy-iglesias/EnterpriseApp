using System.Security.Claims;
using EnterpriseApp.Application.Common.Authorization;
using EnterpriseApp.Domain.Authorization;
using Microsoft.AspNetCore.Authorization;

namespace EnterpriseApp.Infrastructure.Authorization;
// PermissionRequirement is in this same namespace (EnterpriseApp.Infrastructure.Authorization)

/// <summary>
/// Validates a <see cref="PermissionRequirement"/> against the authenticated principal's
/// <c>perms</c> claim. The principal already carries the resolved permission set
/// from the JWT — no DB hit per request, only a cache fallback if the claim is missing.
/// </summary>
public sealed class PermissionAuthorizationHandler(IPermissionService permissionService)
    : AuthorizationHandler<PermissionRequirement>
{
    protected override async Task HandleRequirementAsync(
        AuthorizationHandlerContext context,
        PermissionRequirement       requirement)
    {
        if (context.User.Identity?.IsAuthenticated != true) return;

        var userPerms = ExtractPermissionClaims(context.User);

        // Wildcard → super-admin gets everything.
        if (userPerms.Contains(PermissionCodes.Wildcard))
        {
            context.Succeed(requirement);
            return;
        }

        // ALL required permissions must be present (AND semantics within one attribute).
        var allMatch = requirement.Permissions.All(p => userPerms.Contains(p));
        if (allMatch)
        {
            context.Succeed(requirement);
            return;
        }

        // Fallback: the access token may not carry perms — resolve via service.
        // Useful for legacy clients during the rollout window.
        var userId = context.User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrEmpty(userId)) return;

        var tenantId = TryParseTenant(context.User.FindFirstValue("tid"));
        var resolved = await permissionService.GetPermissionsForUserAsync(userId, tenantId);

        if (resolved.Contains(PermissionCodes.Wildcard) ||
            requirement.Permissions.All(p => resolved.Contains(p)))
        {
            context.Succeed(requirement);
        }
    }

    private static HashSet<string> ExtractPermissionClaims(ClaimsPrincipal user)
    {
        var perms = new HashSet<string>(StringComparer.Ordinal);

        // 1. Multiple "perm" claims
        foreach (var c in user.FindAll("perm"))
            if (!string.IsNullOrWhiteSpace(c.Value)) perms.Add(c.Value);

        // 2. Single "perms" claim with JSON array
        var permsJson = user.FindFirstValue("perms");
        if (!string.IsNullOrWhiteSpace(permsJson))
        {
            try
            {
                var arr = System.Text.Json.JsonSerializer.Deserialize<string[]>(permsJson);
                if (arr is not null) foreach (var p in arr) perms.Add(p);
            }
            catch (System.Text.Json.JsonException) { /* ignore malformed claim */ }
        }
        return perms;
    }

    private static Guid? TryParseTenant(string? raw) =>
        Guid.TryParse(raw, out var guid) ? guid : null;
}
