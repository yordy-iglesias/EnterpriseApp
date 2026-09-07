using EnterpriseApp.Domain.Common;
using MediatR;

namespace EnterpriseApp.Application.Features.Identity.Users.Commands.CreateUser;

public sealed record CreateUserCommand(
    string Email,
    string Password,
    string FirstName,
    string LastName,
    Guid?  TenantId) : IRequest<Result<Guid>>;
