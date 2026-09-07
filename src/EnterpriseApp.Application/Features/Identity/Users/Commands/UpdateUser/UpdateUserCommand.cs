using EnterpriseApp.Domain.Common;
using MediatR;

namespace EnterpriseApp.Application.Features.Identity.Users.Commands.UpdateUser;

public sealed record UpdateUserCommand(
    Guid   Id,
    string FirstName,
    string LastName) : IRequest<Result>;
