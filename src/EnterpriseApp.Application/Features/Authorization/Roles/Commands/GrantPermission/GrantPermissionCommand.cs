using EnterpriseApp.Domain.Common;
using MediatR;

namespace EnterpriseApp.Application.Features.Authorization.Roles.Commands.GrantPermission;

/// <summary>Grants <c>PermissionCode</c> to <c>RoleId</c>. Sensitive operation — audited.</summary>
public sealed record GrantPermissionCommand(
    Guid    RoleId,
    string  PermissionCode,
    DateTimeOffset? ExpiresAt
) : IRequest<Result>;
