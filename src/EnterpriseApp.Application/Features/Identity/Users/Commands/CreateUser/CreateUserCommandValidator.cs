using FluentValidation;

namespace EnterpriseApp.Application.Features.Identity.Users.Commands.CreateUser;

public sealed class CreateUserCommandValidator : AbstractValidator<CreateUserCommand>
{
    public CreateUserCommandValidator()
    {
        RuleFor(x => x.Email).NotEmpty().EmailAddress().MaximumLength(254);
        RuleFor(x => x.Password).NotEmpty().MinimumLength(8)
            .Matches("[A-Z]").WithMessage("Must contain uppercase.")
            .Matches("[a-z]").WithMessage("Must contain lowercase.")
            .Matches("[0-9]").WithMessage("Must contain digit.");
        RuleFor(x => x.FirstName).NotEmpty().MaximumLength(100);
        RuleFor(x => x.LastName).NotEmpty().MaximumLength(100);
    }
}
