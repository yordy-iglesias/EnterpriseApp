using EnterpriseApp.Domain.Common;
using MediatR;

namespace EnterpriseApp.Application.Features.Authorization.Roles.Commands.UnassignRoleFromUser;

public sealed record UnassignRoleFromUserCommand(
    string UserId,
    Guid   RoleId
) : IRequest<Result>;
