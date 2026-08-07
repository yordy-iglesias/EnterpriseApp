using EnterpriseApp.Application.Common.Authorization;
using EnterpriseApp.Application.Features.Authorization.DTOs;
using EnterpriseApp.Domain.Common;
using EnterpriseApp.Domain.Interfaces.Repositories;
using MediatR;

namespace EnterpriseApp.Application.Features.Authorization.Permissions.Queries.GetPermissions;

public sealed class GetPermissionsQueryHandler(
    IUnitOfWork  uow,
    ICurrentUser currentUser)
    : IRequestHandler<GetPermissionsQuery, Result<IReadOnlyList<PermissionDto>>>
{
    public async Task<Result<IReadOnlyList<PermissionDto>>> Handle(
        GetPermissionsQuery query, CancellationToken ct)
    {
        // Read both global (tenantId == null) and tenant-scoped permissions.
        var global = await uow.Permissions.GetAllAsync(tenantId: null, ct);
        var tenantPerms = currentUser.TenantId is { } tid
            ? await uow.Permissions.GetAllAsync(tid, ct)
            : [];

        var combined = global.Concat(tenantPerms);

        if (!string.IsNullOrWhiteSpace(query.Module))
            combined = combined.Where(p => p.Code.Module == query.Module);

        var dtos = combined
            .OrderBy(p => p.Code.Module).ThenBy(p => p.Code.Action)
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

        return Result.Success<IReadOnlyList<PermissionDto>>(dtos);
    }
}
