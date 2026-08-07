using EnterpriseApp.Domain.Common;

namespace EnterpriseApp.Domain.Entities.Authorization;

/// <summary>
/// Append-only audit record for any change in roles, permissions or user-role assignments.
/// <para>Persisted as immutable: Infrastructure adds a DB trigger preventing UPDATE/DELETE.
/// See <c>.claude/rules/audit-logging.md §3</c>.</para>
/// </summary>
public sealed class PermissionAuditLog : BaseEntity
{
    private PermissionAuditLog() { }

    public static PermissionAuditLog Record(
        PermissionAuditAction action,
        string                performedBy,
        string?               performedByEmail,
        string?               targetUserId,
        RoleId?               targetRoleId,
        PermissionId?         targetPermissionId,
        Guid?                 tenantId,
        string?               ipAddress,
        string?               userAgent,
        string?               correlationId,
        string?               reason,
        string?               metadataJson)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(performedBy);

        return new PermissionAuditLog
        {
            Id                  = Guid.NewGuid(),
            PerformedAt         = DateTimeOffset.UtcNow,
            Action              = action,
            PerformedBy         = performedBy,
            PerformedByEmail    = performedByEmail,
            TargetUserId        = targetUserId,
            TargetRoleId        = targetRoleId,
            TargetPermissionId  = targetPermissionId,
            TenantId            = tenantId,
            IpAddress           = ipAddress,
            UserAgent           = userAgent,
            CorrelationId       = correlationId,
            Reason              = reason,
            MetadataJson        = metadataJson,
        };
    }

    public Guid                  Id                 { get; private set; }
    public DateTimeOffset        PerformedAt        { get; private set; }
    public PermissionAuditAction Action             { get; private set; }
    public string                PerformedBy        { get; private set; } = string.Empty;
    public string?               PerformedByEmail   { get; private set; }
    public string?               TargetUserId       { get; private set; }
    public RoleId?               TargetRoleId       { get; private set; }
    public PermissionId?         TargetPermissionId { get; private set; }
    public Guid?                 TenantId           { get; private set; }
    public string?               IpAddress          { get; private set; }
    public string?               UserAgent          { get; private set; }
    public string?               CorrelationId      { get; private set; }
    public string?               Reason             { get; private set; }
    public string?               MetadataJson       { get; private set; }
}

/// <summary>Discrete kinds of authorization events tracked in <see cref="PermissionAuditLog"/>.</summary>
public enum PermissionAuditAction
{
    UserRoleAssigned        = 1,
    UserRoleUnassigned      = 2,
    PermissionGranted       = 3,
    PermissionRevoked       = 4,
    RoleCreated             = 5,
    RoleUpdated             = 6,
    RoleDeleted             = 7,
    PermissionCreated       = 8,
    PermissionUpdated       = 9,
    PermissionDeleted       = 10,
    UserActivated           = 11,
    UserDeactivated         = 12,
    AccessDeniedSensitive   = 13,
}
