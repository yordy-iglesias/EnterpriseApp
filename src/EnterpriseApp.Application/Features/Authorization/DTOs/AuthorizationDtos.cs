namespace EnterpriseApp.Application.Features.Authorization.DTOs;

public sealed record RoleDto(
    Guid    Id,
    string  Name,
    string? Description,
    Guid?   TenantId,
    Guid?   ParentRoleId,
    bool    IsSystem,
    DateTimeOffset CreatedAt,
    int     PermissionsCount);

public sealed record RoleDetailDto(
    Guid    Id,
    string  Name,
    string? Description,
    Guid?   TenantId,
    Guid?   ParentRoleId,
    bool    IsSystem,
    DateTimeOffset CreatedAt,
    DateTimeOffset? PermissionsSnapshotUpdatedAt,
    IReadOnlyList<PermissionDto> Permissions);

public sealed record PermissionDto(
    Guid    Id,
    string  Code,
    string  Module,
    string  Action,
    string? Qualifier,
    string  DisplayName,
    string? Description,
    bool    IsSensitive,
    bool    IsSystem,
    Guid?   TenantId);

public sealed record UserPermissionsDto(
    string                       UserId,
    Guid?                        TenantId,
    IReadOnlyList<string>        Roles,
    IReadOnlyList<string>        Permissions,
    string                       SnapshotVersion);
