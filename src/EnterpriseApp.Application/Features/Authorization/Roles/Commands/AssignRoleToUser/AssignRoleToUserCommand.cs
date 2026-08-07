using EnterpriseApp.Domain.Common;
using MediatR;

namespace EnterpriseApp.Application.Features.Authorization.Roles.Commands.AssignRoleToUser;

public sealed record AssignRoleToUserCommand(
    string UserId,
    Guid   RoleId
) : IRequest<Result>;
