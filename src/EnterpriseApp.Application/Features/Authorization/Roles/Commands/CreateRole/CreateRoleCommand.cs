using EnterpriseApp.Domain.Common;
using MediatR;

namespace EnterpriseApp.Application.Features.Authorization.Roles.Commands.CreateRole;

/// <summary>Creates a non-system role within the caller's tenant.</summary>
public sealed record CreateRoleCommand(
    string  Name,
    string? Description,
    Guid?   ParentRoleId
) : IRequest<Result<Guid>>;
