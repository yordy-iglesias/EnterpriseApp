using EnterpriseApp.Application.Common.Authorization;
using EnterpriseApp.Application.Common.Errors;
using EnterpriseApp.Domain.Common;
using EnterpriseApp.Domain.Entities.Authorization;
using EnterpriseApp.Domain.Interfaces.Repositories;
using MediatR;

namespace EnterpriseApp.Application.Features.Authorization.Roles.Commands.UnassignRoleFromUser;

public sealed class UnassignRoleFromUserCommandHandler(
    IUnitOfWork           uow,
    ICurrentUser          currentUser,
    IPublisher            publisher)
    : IRequestHandler<UnassignRoleFromUserCommand, Result>
{
    public async Task<Result> Handle(UnassignRoleFromUserCommand cmd, CancellationToken ct)
    {
        var roleId   = RoleId.From(cmd.RoleId);
        var existing = await uow.UserRoles.GetAsync(cmd.UserId, roleId, ct);
        if (existing is null)
            return Result.Failure(AuthorizationErrors.UserDoesNotHaveRole);

        if (existing.TenantId != currentUser.TenantId && currentUser.TenantId is not null)
            return Result.Failure(AuthorizationErrors.TenantMismatch);

        var unassignedBy = currentUser.UserId ?? "system";
        var events       = existing.Unassign(unassignedBy).ToList();

        uow.UserRoles.Remove(existing);
        await uow.SaveChangesAsync(ct);

        // Domain events from a removed entity must be published manually since the
        // change tracker no longer holds the instance.
        foreach (var evt in events)
            await publisher.Publish(evt, ct);

        return Result.Success();
    }
}
