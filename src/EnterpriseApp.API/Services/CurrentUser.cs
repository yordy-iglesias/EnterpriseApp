using System.Security.Claims;
using System.Text.Json;
using EnterpriseApp.Application.Common.Authorization;
using EnterpriseApp.Domain.Authorization;

namespace EnterpriseApp.API.Services;

/// <summary>
/// Rich principal abstraction reading from <see cref="HttpContext"/>.
/// Reads <c>perms</c> (JSON array) or repeated <c>perm</c> claims, plus tenant id (<c>tid</c>),
/// AMR list, and <c>auth_time</c>. Implements <see cref="ICurrentUser"/>.
/// </summary>
public sealed class CurrentUser(IHttpContextAccessor httpContextAccessor) : ICurrentUser
{
    private ClaimsPrincipal? Principal => httpContextAccessor.HttpContext?.User;

    public string? UserId =>
        Principal?.FindFirstValue(ClaimTypes.NameIdentifier)
        ?? Principal?.FindFirstValue("sub");

    public string? UserName =>
        Principal?.FindFirstValue(ClaimTypes.Name)
        ?? Principal?.FindFirstValue("name");

    public string? Email =>
        Principal?.FindFirstValue(ClaimTypes.Email)
        ?? Principal?.FindFirstValue("email");

    public Guid? TenantId
    {
        get
        {
            var raw = Principal?.FindFirstValue("tid");
            return Guid.TryParse(raw, out var g) ? g : null;
        }
    }

    public bool IsAuthenticated => Principal?.Identity?.IsAuthenticated == true;

    public IReadOnlySet<string> Permissions => _permissions ??= ResolvePermissions();
    private HashSet<string>? _permissions;

    public IReadOnlySet<string> AuthenticationMethods =>
        Principal?.FindAll("amr").Select(c => c.Value).ToHashSet(StringComparer.OrdinalIgnoreCase) ?? [];

    public DateTimeOffset? MfaVerifiedAt
    {
        get
        {
            if (!AuthenticationMethods.Contains("mfa") && !AuthenticationMethods.Contains("otp"))
                return null;
            var raw = Principal?.FindFirstValue("auth_time");
            return long.TryParse(raw, out var unix)
                ? DateTimeOffset.FromUnixTimeSeconds(unix)
                : null;
        }
    }

    public bool HasPermission(string code) =>
        Permissions.Contains(PermissionCodes.Wildcard) || Permissions.Contains(code);

    public bool HasAnyPermission(params string[] codes) =>
        codes.Any(HasPermission);

    public bool HasAllPermissions(params string[] codes) =>
        codes.All(HasPermission);

    private HashSet<string> ResolvePermissions()
    {
        var set = new HashSet<string>(StringComparer.Ordinal);

        // Multiple "perm" claims
        foreach (var c in Principal?.FindAll("perm") ?? Enumerable.Empty<Claim>())
            if (!string.IsNullOrWhiteSpace(c.Value)) set.Add(c.Value);

        // Single "perms" JSON-array claim
        var permsJson = Principal?.FindFirstValue("perms");
        if (!string.IsNullOrWhiteSpace(permsJson))
        {
            try
            {
                var arr = JsonSerializer.Deserialize<string[]>(permsJson);
                if (arr is not null) foreach (var p in arr) set.Add(p);
            }
            catch (JsonException) { /* malformed claim */ }
        }
        return set;
    }
}
