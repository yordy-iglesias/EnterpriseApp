using EnterpriseApp.Application.Common.Authorization;
using EnterpriseApp.Application.Common.Errors;
using EnterpriseApp.Domain.Common;
using EnterpriseApp.Domain.Entities.Authorization;
using EnterpriseApp.Domain.Interfaces.Repositories;
using MediatR;

namespace EnterpriseApp.Application.Features.Authorization.Roles.Commands.CreateRole;

public sealed class CreateRoleCommandHandler(
    IUnitOfWork uow,
    ICurrentUser currentUser)
    : IRequestHandler<CreateRoleCommand, Result<Guid>>
{
    public async Task<Result<Guid>> Handle(CreateRoleCommand cmd, CancellationToken ct)
    {
        var normalized = cmd.Name.Trim().ToUpperInvariant();
        var tenantId   = currentUser.TenantId;

        if (await uow.Roles.GetByNameAsync(normalized, tenantId, ct) is not null)
            return Result.Failure<Guid>(AuthorizationErrors.RoleAlreadyExists);

        RoleId? parent = cmd.ParentRoleId is { } pid ? RoleId.From(pid) : null;
        if (parent is not null && !await uow.Roles.ExistsAsync(parent, ct))
            return Result.Failure<Guid>(AuthorizationErrors.RoleNotFound);

        var role = Role.Create(
            name:           cmd.Name.Trim().ToLowerInvariant(),
            normalizedName: normalized,
            description:    cmd.Description,
            tenantId:       tenantId,
            parentRoleId:   parent,
            isSystem:       false,
            createdBy:      currentUser.UserId);

        await uow.Roles.AddAsync(role, ct);
        await uow.SaveChangesAsync(ct);

        return Result.Success(role.Id.Value);
    }
}
