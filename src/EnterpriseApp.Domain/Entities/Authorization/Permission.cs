using EnterpriseApp.Domain.Common;
using EnterpriseApp.Domain.Exceptions;
using EnterpriseApp.Domain.ValueObjects;

namespace EnterpriseApp.Domain.Entities.Authorization;

/// <summary>
/// A single, atomic capability — e.g. <c>module.view</c>, <c>module.sign</c>.
/// <para>Stored in DB so admins can introduce permissions without redeploys, addressing the
/// hardcoded <c>ModuleEnum</c>/<c>PermissionEnum</c> .</para>
/// </summary>
public sealed class Permission : AuditableEntity<PermissionId>
{
    private Permission() { }                                // EF Core
    private Permission(PermissionId id) : base(id) { }     // factory path

    /// <summary>Factory — validates the code via <see cref="PermissionCode"/>.</summary>
    public static Permission Create(
        string  code,
        string  displayName,
        string? description,
        bool    isSensitive,
        bool    isSystem,
        Guid?   tenantId,
        string? createdBy)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(displayName);
        var parsed = PermissionCode.From(code);

        return new Permission(PermissionId.New())
        {
            Code        = parsed,
            DisplayName = displayName.Trim(),
            Description = description?.Trim(),
            IsSensitive = isSensitive,
            IsSystem    = isSystem,
            TenantId    = tenantId,
            CreatedBy   = createdBy,
        };
    }

    public PermissionCode Code        { get; private set; } = default!;
    public string         DisplayName { get; private set; } = string.Empty;
    public string?        Description { get; private set; }

    /// <summary>Requires step-up MFA. See <c>.claude/rules/authorization.md §5.3</c>.</summary>
    public bool IsSensitive { get; private set; }

    /// <summary>System permissions cannot be deleted (e.g. <c>role.assign</c>).</summary>
    public bool IsSystem { get; private set; }

    /// <summary><c>null</c> means cross-tenant / global permission.</summary>
    public Guid? TenantId { get; private set; }

    public void UpdateMetadata(string displayName, string? description, string? updatedBy)
    {
        if (IsSystem)
            throw new DomainException("System permissions cannot be modified.");

        ArgumentException.ThrowIfNullOrWhiteSpace(displayName);
        DisplayName = displayName.Trim();
        Description = description?.Trim();
        UpdatedBy   = updatedBy;
        UpdatedAt   = DateTimeOffset.UtcNow;
    }

    public void MarkSensitive(string? updatedBy)
    {
        if (IsSensitive) return;
        IsSensitive = true;
        UpdatedBy   = updatedBy;
        UpdatedAt   = DateTimeOffset.UtcNow;
    }
}

/// <summary>Strongly-typed Id for <see cref="Permission"/>.</summary>
public sealed record PermissionId(Guid Value)
{
    public static PermissionId New()             => new(Guid.NewGuid());
    public static PermissionId From(Guid value)  => new(value);
    public override string ToString()            => Value.ToString();
}
