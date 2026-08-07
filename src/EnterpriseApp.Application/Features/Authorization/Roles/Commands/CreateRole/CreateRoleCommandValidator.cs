using FluentValidation;

namespace EnterpriseApp.Application.Features.Authorization.Roles.Commands.CreateRole;

public sealed class CreateRoleCommandValidator : AbstractValidator<CreateRoleCommand>
{
    public CreateRoleCommandValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("Role name is required.")
            .Matches("^[a-z][a-z0-9-]*$").WithMessage("Role name must be kebab-case (lowercase letters, digits, hyphens).")
            .MaximumLength(64);

        RuleFor(x => x.Description)
            .MaximumLength(512)
            .When(x => x.Description is not null);
    }
}
