using EnterpriseApp.Application.Common.Authorization;
using EnterpriseApp.Application.Common.Errors;
using EnterpriseApp.Domain.Common;
using EnterpriseApp.Domain.Entities.Authorization;
using EnterpriseApp.Domain.Interfaces.Repositories;
using EnterpriseApp.Domain.ValueObjects;
using MediatR;

namespace EnterpriseApp.Application.Features.Authorization.Roles.Commands.RevokePermission;

public sealed class RevokePermissionCommandHandler(
    IUnitOfWork        uow,
    IPermissionService permissionService,
    ICurrentUser       currentUser)
    : IRequestHandler<RevokePermissionCommand, Result>
{
    public async Task<Result> Handle(RevokePermissionCommand cmd, CancellationToken ct)
    {
        if (!PermissionCode.TryFrom(cmd.PermissionCode, out var code) || code is null)
            return Result.Failure(Error.Validation(nameof(cmd.PermissionCode), "Invalid permission code format."));

        var role = await uow.Roles.GetWithPermissionsAsync(RoleId.From(cmd.RoleId), ct);
        if (role is null)               return Result.Failure(AuthorizationErrors.RoleNotFound);
        if (role.IsSystem)              return Result.Failure(AuthorizationErrors.RoleIsSystem);
        if (role.TenantId != currentUser.TenantId && currentUser.TenantId is not null)
            return Result.Failure(AuthorizationErrors.TenantMismatch);

        var permission = await uow.Permissions.GetByCodeAsync(code, role.TenantId, ct)
                       ?? await uow.Permissions.GetByCodeAsync(code, tenantId: null, ct);
        if (permission is null)         return Result.Failure(AuthorizationErrors.PermissionNotFound);

        try
        {
            role.RevokePermission(permission.Id, currentUser.UserId ?? "system");
        }
        catch (Domain.Exceptions.DomainException ex)
        {
            return Result.Failure(new Error("Permission.RevokeFailed", ex.Message));
        }

        var rolePermissionIds = role.RolePermissions.Select(rp => rp.PermissionId).ToArray();
        var resolved = await uow.Permissions.GetAllAsync(role.TenantId, ct);
        var resolvedCodes = resolved
            .Where(p => rolePermissionIds.Contains(p.Id))
            .Select(p => p.Code.Value)
            .ToArray();

        role.RefreshPermissionsSnapshot(
            permissionService.SignRoleSnapshot(resolvedCodes, role.Name));

        uow.Roles.Update(role);
        await uow.SaveChangesAsync(ct);
        return Result.Success();
    }
}
