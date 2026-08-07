using EnterpriseApp.Application.Common.Authorization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Options;

namespace EnterpriseApp.Infrastructure.Authorization;

/// <summary>
/// Materialises authorization policies on-demand from the <c>perm:{code1,code2}</c>
/// policy-name convention used by <see cref="RequirePermissionAttribute"/>.
/// <para>This avoids registering one policy per permission code at startup —
/// new permissions become enforceable as soon as they are used in an attribute.</para>
/// </summary>
public sealed class PermissionPolicyProvider : IAuthorizationPolicyProvider
{
    private readonly DefaultAuthorizationPolicyProvider _fallback;

    public PermissionPolicyProvider(IOptions<AuthorizationOptions> options)
    {
        _fallback = new DefaultAuthorizationPolicyProvider(options);
    }

    public Task<AuthorizationPolicy> GetDefaultPolicyAsync()       => _fallback.GetDefaultPolicyAsync();
    public Task<AuthorizationPolicy?> GetFallbackPolicyAsync()     => _fallback.GetFallbackPolicyAsync();

    public Task<AuthorizationPolicy?> GetPolicyAsync(string policyName)
    {
        if (policyName.StartsWith(AuthorizationPolicyConstants.PermissionPolicyPrefix, StringComparison.Ordinal))
        {
            var codes = policyName[AuthorizationPolicyConstants.PermissionPolicyPrefix.Length..]
                .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

            var policy = new AuthorizationPolicyBuilder()
                .RequireAuthenticatedUser()
                .AddRequirements(new PermissionRequirement(codes))
                .Build();
            return Task.FromResult<AuthorizationPolicy?>(policy);
        }

        if (string.Equals(policyName, AuthorizationPolicyConstants.StepUpMfaPolicyName, StringComparison.Ordinal))
        {
            var policy = new AuthorizationPolicyBuilder()
                .RequireAuthenticatedUser()
                .AddRequirements(new StepUpMfaRequirement(AuthorizationPolicyConstants.StepUpMfaMaxAge))
                .Build();
            return Task.FromResult<AuthorizationPolicy?>(policy);
        }

        return _fallback.GetPolicyAsync(policyName);
    }
}
