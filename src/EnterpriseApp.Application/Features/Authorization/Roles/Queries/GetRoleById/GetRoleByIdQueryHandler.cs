using EnterpriseApp.Application.Common.Authorization;
using EnterpriseApp.Application.Common.Errors;
using EnterpriseApp.Application.Features.Authorization.DTOs;
using EnterpriseApp.Domain.Common;
using EnterpriseApp.Domain.Entities.Authorization;
using EnterpriseApp.Domain.Interfaces.Repositories;
using MediatR;

namespace EnterpriseApp.Application.Features.Authorization.Roles.Queries.GetRoleById;

public sealed class GetRoleByIdQueryHandler(
    IUnitOfWork  uow,
    ICurrentUser currentUser)
    : IRequestHandler<GetRoleByIdQuery, Result<RoleDetailDto>>
{
    public async Task<Result<RoleDetailDto>> Handle(GetRoleByIdQuery query, CancellationToken ct)
    {
        var role = await uow.Roles.GetWithPermissionsAsync(RoleId.From(query.RoleId), ct);
        if (role is null) return Result.Failure<RoleDetailDto>(AuthorizationErrors.RoleNotFound);
        if (role.TenantId != currentUser.TenantId && currentUser.TenantId is not null)
            return Result.Failure<RoleDetailDto>(AuthorizationErrors.TenantMismatch);

        var allPermissions = await uow.Permissions.GetAllAsync(role.TenantId, ct);
        var ids = role.RolePermissions.Select(rp => rp.PermissionId).ToHashSet();

        var permissions = allPermissions
            .Where(p => ids.Contains(p.Id))
            .Select(p => new PermissionDto(
                p.Id.Value,
                p.Code.Value,
                p.Code.Module,
                p.Code.Action,
                p.Code.Qualifier,
                p.DisplayName,
                p.Description,
                p.IsSensitive,
                p.IsSystem,
                p.TenantId))
            .ToArray();

        return Result.Success(new RoleDetailDto(
            role.Id.Value,
            role.Name,
            role.Description,
            role.TenantId,
            role.ParentRoleId?.Value,
            role.IsSystem,
            role.CreatedAt,
            role.PermissionsSnapshotUpdatedAt,
            permissions));
    }
}
