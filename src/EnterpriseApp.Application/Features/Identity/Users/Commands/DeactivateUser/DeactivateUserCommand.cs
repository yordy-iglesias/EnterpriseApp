using EnterpriseApp.Domain.Common;
using MediatR;

namespace EnterpriseApp.Application.Features.Identity.Users.Commands.DeactivateUser;

public sealed record DeactivateUserCommand(Guid Id) : IRequest<Result>;
