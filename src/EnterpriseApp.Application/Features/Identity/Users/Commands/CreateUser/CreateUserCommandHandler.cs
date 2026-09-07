using EnterpriseApp.Application.Common.Authorization;
using EnterpriseApp.Application.Common.Errors;
using EnterpriseApp.Application.Common.Interfaces;
using EnterpriseApp.Domain.Common;
using EnterpriseApp.Domain.Entities.Identity;
using EnterpriseApp.Domain.Interfaces.Repositories;
using EnterpriseApp.Domain.ValueObjects;
using MediatR;

namespace EnterpriseApp.Application.Features.Identity.Users.Commands.CreateUser;

internal sealed class CreateUserCommandHandler(
    IUnitOfWork     uow,
    IPasswordHasher hasher,
    ICurrentUser    currentUser)
    : IRequestHandler<CreateUserCommand, Result<Guid>>
{
    public async Task<Result<Guid>> Handle(CreateUserCommand cmd, CancellationToken ct)
    {
        var normalized = cmd.Email.Trim().ToLowerInvariant();
        if (await uow.Users.ExistsByEmailAsync(normalized, ct))
            return Result.Failure<Guid>(AuthErrors.EmailAlreadyExists);

        var email = Email.From(cmd.Email);
        var user  = User.Create(
            email:        email,
            passwordHash: hasher.Hash(cmd.Password),
            firstName:    cmd.FirstName,
            lastName:     cmd.LastName,
            tenantId:     cmd.TenantId,
            createdBy:    currentUser.UserId);

        await uow.Users.AddAsync(user, ct);
        await uow.SaveChangesAsync(ct);
        return Result.Success(user.Id.Value);
    }
}
