using EnterpriseApp.Domain.Common;
using MediatR;

namespace EnterpriseApp.Application.Features.Authorization.Roles.Commands.RevokePermission;

public sealed record RevokePermissionCommand(
    Guid   RoleId,
    string PermissionCode
) : IRequest<Result>;
