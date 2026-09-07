using EnterpriseApp.Domain.Common;
using MediatR;

namespace EnterpriseApp.Application.Features.Identity.Users.Commands.ActivateUser;

public sealed record ActivateUserCommand(Guid Id) : IRequest<Result>;
