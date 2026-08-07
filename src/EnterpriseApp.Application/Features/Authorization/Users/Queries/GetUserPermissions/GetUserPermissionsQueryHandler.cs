using EnterpriseApp.Application.Common.Authorization;
using EnterpriseApp.Application.Features.Authorization.DTOs;
using EnterpriseApp.Domain.Common;
using EnterpriseApp.Domain.Interfaces.Repositories;
using MediatR;

namespace EnterpriseApp.Application.Features.Authorization.Users.Queries.GetUserPermissions;

/// <summary>
/// Returns the resolved permission set for a user — used by the API layer to embed
/// the <c>perms</c> claim in newly-issued JWTs and to render the admin UI.
/// </summary>
public sealed class GetUserPermissionsQueryHandler(
    IUnitOfWork        uow,
    IPermissionService permissions,
    ICurrentUser       currentUser)
    : IRequestHandler<GetUserPermissionsQuery, Result<UserPermissionsDto>>
{
    public async Task<Result<UserPermissionsDto>> Handle(
        GetUserPermissionsQuery query, CancellationToken ct)
    {
        var roles    = await uow.Roles.GetByUserIdAsync(query.UserId, ct);
        var perms    = await permissions.GetPermissionsForUserAsync(query.UserId, currentUser.TenantId, ct);
        var version  = permissions.ComputeSnapshotVersion(perms);

        return Result.Success(new UserPermissionsDto(
            UserId:          query.UserId,
            TenantId:        currentUser.TenantId,
            Roles:           roles.Select(r => r.Name).ToArray(),
            Permissions:     perms.OrderBy(p => p, StringComparer.Ordinal).ToArray(),
            SnapshotVersion: version));
    }
}
