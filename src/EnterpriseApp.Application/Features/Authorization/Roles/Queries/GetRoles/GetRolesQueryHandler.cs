using EnterpriseApp.Application.Common.Authorization;
using EnterpriseApp.Application.Features.Authorization.DTOs;
using EnterpriseApp.Domain.Common;
using EnterpriseApp.Domain.Interfaces.Repositories;
using MediatR;

namespace EnterpriseApp.Application.Features.Authorization.Roles.Queries.GetRoles;

public sealed class GetRolesQueryHandler(
    IUnitOfWork  uow,
    ICurrentUser currentUser)
    : IRequestHandler<GetRolesQuery, Result<IReadOnlyList<RoleDto>>>
{
    public async Task<Result<IReadOnlyList<RoleDto>>> Handle(GetRolesQuery _, CancellationToken ct)
    {
        var roles = await uow.Roles.GetByTenantAsync(currentUser.TenantId, ct);
        var dtos  = roles
            .Select(r => new RoleDto(
                r.Id.Value,
                r.Name,
                r.Description,
                r.TenantId,
                r.ParentRoleId?.Value,
                r.IsSystem,
                r.CreatedAt,
                r.RolePermissions.Count))
            .ToArray();
        return Result.Success<IReadOnlyList<RoleDto>>(dtos);
    }
}
